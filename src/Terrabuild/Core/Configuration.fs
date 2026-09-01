module Configuration
module ExtensionRegistry = Terrabuild.Extensions.ScriptRegistry
open System.IO
open Collections
open System
open System.Collections.Concurrent
open System.Runtime.ExceptionServices
open System.Threading
open System.Threading.Tasks
open Terrabuild.Scripting
open Terrabuild.ScriptingContracts
open Terrabuild.Expression
open Errors
open Terrabuild.PubSub
open Microsoft.Extensions.FileSystemGlobbing
open Serilog
open Terrabuild.Configuration
open System.Runtime.InteropServices
open GraphDef

[<RequireQualifiedAccess>]
type TargetStep = {
    Hash: string
    Image: string option
    Platform: string option
    Cpus: int option
    ContainerVariables: string set
    Envs: Map<string, string>
    Extension: string
    Command: string
    Script: Terrabuild.Scripting.Script
    Context: Value
}

[<RequireQualifiedAccess>]
type Target = {
    Hash: string
    ClusterHash: string
    Build: BuildMode option
    Batch: BatchMode
    Phase: string option
    Lock: string option
    DependsOn: string set
    Outputs: string set
    Cache: ArtifactMode option
    EvaluationInputs: EvaluationInput list
    EnvironmentSensitive: bool option
    Steps: TargetStep list
}


[<RequireQualifiedAccess>]
type Project = {
    Id: string
    Name: string option
    Directory: string
    Hash: string
    Dependencies: string set
    Files: string set
    Targets: Map<string, Target>
    Labels: string set
    Types: string set
}

[<RequireQualifiedAccess>]
type Workspace = {
    // Space to use
    Id: string option

    // Computed projects selection (derived from user inputs)
    SelectedProjects: string set

    // All targets at workspace level
    Targets: Map<string, Set<string>>

    // Declared phase dependency graph
    Phases: Map<string, Set<string>>

    // All discovered projects in workspace
    Projects: Map<string, Project>
}

type private LazyScript = Lazy<Terrabuild.Scripting.Script>

[<RequireQualifiedAccess>]
type private LoadedProject = {
    Id: string
    Type: string
    Name: string option
    DependsOn: string set
    Dependencies: string set
    Includes: string set
    Ignores: string set
    Outputs: string set
    Targets: Map<string, AST.Project.TargetBlock>
    Labels: string set
    Initializers: string set
    Extensions: Map<string, AST.ExtensionBlock>
    Scripts: Map<string, LazyScript>
    Locals: Map<string, Expr>
}


let scanFolders root (ignores: Set<string>) =
    let matcher = Matcher()
    matcher.AddInclude("**/*").AddExcludePatterns(ignores)

    fun dir ->
        // exclude sub-folders with WORKSPACE
        let relativeDir = dir |> FS.relativePath root
        // Matcher is configured once and shared by the parallel directory walk.
        if lock matcher (fun () -> matcher.Match(relativeDir).HasMatches) then
            match FS.combinePath dir "WORKSPACE" with
            | FS.File _ -> false
            | _ -> true
        else
            false


let internal scanProjectDirectories maxConcurrency root scanFolder loadProject =
    if maxConcurrency <= 0 then
        invalidArg (nameof maxConcurrency) "maxConcurrency must be > 0"

    use directories = new BlockingCollection<string>(ConcurrentQueue<string>())
    let errors = ConcurrentQueue<ExceptionDispatchInfo>()
    let mutable pending = 1
    directories.Add(root)

    let completeDirectory () =
        if Interlocked.Decrement(&pending) = 0 then
            directories.CompleteAdding()

    let enqueueDirectory directory =
        Interlocked.Increment(&pending) |> ignore
        directories.Add(directory)

    let scan () =
        for dir in directories.GetConsumingEnumerable() do
            try
                try
                    if errors.IsEmpty && (dir = root || scanFolder dir) then
                        match FS.combinePath dir "PROJECT" with
                        | FS.File _ -> loadProject dir
                        | _ ->
                            for subdir in IO.enumerateDirs dir do
                                enqueueDirectory subdir
                with exn ->
                    errors.Enqueue(ExceptionDispatchInfo.Capture exn)
            finally
                completeDirectory ()

    Parallel.For(0, maxConcurrency, fun _ -> scan ()) |> ignore

    match errors.TryDequeue() with
    | true, error -> error.Throw()
    | _ -> ()


let (|Bool|Number|String|) (value: string) = 
    match value |> Boolean.TryParse with
    | true, value -> Bool value
    | _ ->
        match value |> Int32.TryParse with
        | true, value -> Number value
        | _ -> String value

let default_ignores = Set [
    "node_modules"
    ".pnpm-store"
    ".terrabuild"
    "bin"
    "obj"
    "dist"
]

let default_script_deny_globs = [ ".git" ]

[<Literal>]
let private SCOPE_PATH = "workspace/path"

[<Literal>]
let private SCOPE_NAME = "workspace/name"

let private format_project_id scope id = $"{scope}#{id}"

let private normalizeProjectSelector (value: string) =
    let normalized = value.Trim().Replace('\\', '/').TrimEnd('/').ToLowerInvariant()
    if normalized.StartsWith("./", StringComparison.Ordinal) then normalized[2..]
    else normalized

let private projectSelectors (project: Project) =
    seq {
        yield project.Id |> normalizeProjectSelector
        yield project.Directory |> normalizeProjectSelector
        match project.Name with
        | Some name -> yield name |> normalizeProjectSelector
        | None -> ()
    }
    |> Set.ofSeq

let private resolve_dependency_scope (projectInfo: ProjectInfo) =
    projectInfo.DependencyResolution
    |> Option.defaultValue DependencyResolution.Path

let private buildDeclaredTargetHash (target: AST.Project.TargetBlock) =
    let normalizeExpr = Option.map Expr.StripLocations
    let normalizeStep (step: AST.Project.Step) =
        { step with
            Parameters = step.Parameters |> Map.map (fun _ expr -> expr |> Expr.StripLocations) }

    { target with
        Outputs = target.Outputs |> normalizeExpr
        Build = target.Build |> normalizeExpr
        Cache = target.Cache |> normalizeExpr
        Batch = target.Batch |> normalizeExpr
        Phase = None
        Lock = None
        EnvironmentSensitive = None
        Steps = target.Steps |> List.map normalizeStep }
    |> Json.Serialize
    |> Hash.sha256

let internal buildTargetStepHash extensionName command image platform cpus variables scriptIdentity =
    let containerDependencies =
        match image with
        | Some container ->
            [ yield container
              yield! variables |> Seq.sort
              yield! platform |> Option.toList
              yield! cpus |> Option.map string |> Option.toList ]
        | None -> []

    [ extensionName; command; scriptIdentity ] @ containerDependencies
    |> Hash.sha256strings

let private resolvePhaseReference phaseNames phaseExpr =
    match phaseExpr |> Expr.StripLocations with
    | Expr.Nothing -> None
    | Expr.Variable phaseReference ->
        match phaseReference with
        | String.Regex "^phase\.(.+)$" [ phaseName ] when phaseNames |> Set.contains phaseName -> Some phaseName
        | String.Regex "^phase\.(.+)$" [ phaseName ] -> raiseSymbolError $"Phase '{phaseName}' is not defined in WORKSPACE"
        | _ -> raiseInvalidArg $"Invalid phase reference '{phaseReference}'"
    | _ -> raiseInvalidArg "Expected a phase reference or nothing for phase attribute"

let private buildEvaluationInputs
    (evaluationContext: Eval.EvaluationContext)
    (locals: Map<string, Expr>)
    (target: AST.Project.TargetBlock)
    (extensions: Map<string, AST.ExtensionBlock>) =
    let directDependencies =
        let targetDependencies = Dependencies.reflectionFind { target with EnvironmentSensitive = None }
        target.Steps
        |> Seq.choose (fun step -> extensions |> Map.tryFind step.Extension)
        |> Seq.map Dependencies.reflectionFind
        |> Seq.fold Set.union targetDependencies

    let rec expand pending visited inputs =
        match pending |> Set.toList with
        | [] -> inputs
        | dependency :: _ ->
            let pending = pending |> Set.remove dependency
            if visited |> Set.contains dependency then
                expand pending visited inputs
            else
                let visited = visited |> Set.add dependency
                match dependency with
                | String.Regex "^local\\.(.+)$" [ localName ] ->
                    let localDependencies =
                        locals
                        |> Map.tryFind localName
                        |> Option.map Dependencies.find
                        |> Option.defaultValue Set.empty
                    expand (pending + localDependencies) visited inputs
                | String.Regex "^(terrabuild\\..+|var\\..+)$" [ inputName ] ->
                    expand pending visited (inputs |> Set.add inputName)
                | _ ->
                    expand pending visited inputs

    expand directDependencies Set.empty Set.empty
    |> Seq.choose (fun name ->
        evaluationContext.Data
        |> Map.tryFind name
        |> Option.map (fun value -> {
            EvaluationInput.Name = name
            EvaluationInput.ValueHash = value |> Json.Serialize |> Hash.sha256
        }))
    |> Seq.sortBy _.Name
    |> List.ofSeq

let private buildEvaluationContext (engine: ConfigOptions.Engine) (options: ConfigOptions.Options) (workspaceConfig: AST.Workspace.WorkspaceFile) =
    let tagValue = 
        match options.Label with
        | Some tag -> Value.String tag
        | _ -> Value.Nothing

    let noteValue =
        match options.Note with
        | Some note -> Value.String note
        | _ -> Value.Nothing

    let groupValue =
        match options.GroupId with
        | Some groupId -> Value.String groupId
        | _ -> Value.Nothing

    let terrabuildVars =
        let os =
            if RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then Value.String "darwin"
            elif RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then Value.String "windows"
            elif RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then Value.String "linux"
            else Value.Nothing
        
        let architecture =
            if RuntimeInformation.OSArchitecture = Architecture.Arm64 then Value.String "arm64"
            elif RuntimeInformation.OSArchitecture = Architecture.X64 then Value.String "amd64"
            else Value.Nothing

        let configValue =
            match options.Configuration with
            | Some config -> Value.String config
            | _ -> Value.Nothing

        let envValue =
            match options.Environment with
            | Some env -> Value.String env
            | _ -> Value.Nothing

        Map [ "terrabuild.configuration", configValue
              "terrabuild.environment", envValue
              "terrabuild.branch_or_tag", Value.String options.BranchOrTag 
              "terrabuild.head_commit", Value.String options.HeadCommit.Sha
              "terrabuild.retry", Value.Bool options.Retry 
              "terrabuild.force", Value.Bool options.Force 
              "terrabuild.ci", Value.Bool options.Run.IsSome
              "terrabuild.engine", options.Engine |> string |> String.toLower |> Value.Enum
              "terrabuild.debug", Value.Bool options.Debug 
              "terrabuild.tag", tagValue 
              "terrabuild.note", noteValue
              "terrabuild.group", groupValue
              "terrabuild.os", os 
              "terrabuild.arch", architecture ]
 
    let evaluationContext =
        { Eval.EvaluationContext.WorkspaceDir = Some options.Workspace
          Eval.EvaluationContext.ProjectDir = None
          Eval.EvaluationContext.Data = terrabuildVars }


    // bind variables
    let variables =
        let convertToVarType (name: string) (defaultValue: Value option) (value: string) =
            match value, defaultValue with
            | Bool value, Some (Value.Bool _) -> Value.Bool value
            | Bool value, None -> Value.Bool value
            | Number value, Some (Value.Number _) -> Value.Number value
            | Number value, None -> Value.Number value
            | String value, _ -> Value.String value
            | _ -> raiseTypeError $"Value '{value}' can't be converted to variable '{name}'"

        workspaceConfig.Variables
        |> Map.map (fun name expr ->
            // find dependencies for expression - it must have *no* dependencies for evaluation
            let defaultValue =
                match expr with
                | None -> None
                | Some expr ->
                    let deps = Dependencies.find expr
                    if deps <> Set.empty then raiseInvalidArg $"Default value for variable '{name}' must have no dependencies"
                    expr |> Eval.eval evaluationContext |> Some

            let value =
                match name |> Environment.getTerrabuildEnvVar with
                | Some value -> convertToVarType name defaultValue value |> Some
                | _ ->
                    match options.Variables |> Map.tryFind name with
                    | None -> defaultValue
                    | Some value -> convertToVarType name defaultValue value |> Some

            match value with
            | Some expr -> expr
            | _ -> raiseInvalidArg $"Variable {name} is not initialized")
        |> Seq.map (fun (KeyValue(name, expr)) -> $"var.{name}", expr)
        |> Map.ofSeq

    { evaluationContext with
        Data = evaluationContext.Data |> Map.addMap variables }

let private isHttpScriptUrl (script: string) =
    try
        let uri = System.Uri(script, System.UriKind.Absolute)
        uri.Scheme = System.Uri.UriSchemeHttp || uri.Scheme = System.Uri.UriSchemeHttps
    with
    | :? System.UriFormatException -> false

let private isProtectedBuiltInExtension (extensionName: string) =
    ExtensionRegistry.BuiltInScriptFiles |> Map.containsKey extensionName

let private validateExtensionScriptOverride (extensionName: string) (script: string option) =
    if isProtectedBuiltInExtension extensionName && script.IsSome then
        raiseInvalidArg $"Script override is not allowed for built-in extension '{extensionName}'"


let private buildScripts
    (options: ConfigOptions.Options)
    (workspaceConfig: AST.Workspace.WorkspaceFile)
    (scriptDeniedPathGlobs: string list)
    evaluationContext =
    let normalizeScriptPath currentDir script =
        if isHttpScriptUrl script then script
        else script |> FS.workspaceRelative options.Workspace currentDir

    // load system extensions
    let sysScripts =
        Extensions.SystemExtensions
        |> Map.map (fun _ _ -> None)
        |> Map.map (Extensions.lazyLoadScript options.Workspace scriptDeniedPathGlobs)

    // load user extension
    let userScripts =
        workspaceConfig.Extensions
        |> Map.map (fun extensionName ext ->
            let script =
                ext.Script
                |> Option.bind (Eval.asStringOption << Eval.eval evaluationContext)
            validateExtensionScriptOverride extensionName script
            match script with
            | Some script -> script |> normalizeScriptPath "" |> Some
            | _ -> None)
        |> Map.map (Extensions.lazyLoadScript options.Workspace scriptDeniedPathGlobs)

    let scripts = sysScripts |> Map.addMap userScripts
    scripts

let private addExtensionEntries extensionName field inherited declared =
    match inherited, declared with
    | None, entries
    | entries, None -> entries
    | Some inheritedEntries, Some declaredEntries ->
        let conflicts =
            declaredEntries
            |> Map.keys
            |> Set.ofSeq
            |> Set.intersect (inheritedEntries |> Map.keys |> Set.ofSeq)

        if conflicts |> Set.isEmpty |> not then
            let names = conflicts |> String.concat "', '"
            raiseInvalidArg
                $"Project extension '{extensionName}' cannot replace inherited {field} entries: '{names}'. Project extension collections may only add entries."

        inheritedEntries |> Map.addMap declaredEntries |> Some

let private addExtensionVariables inherited declared =
    match inherited, declared with
    | None, variables
    | variables, None -> variables
    | Some inheritedVariables, Some declaredVariables ->
        Expr.Function (
            Function.Plus,
            [ inheritedVariables; declaredVariables ]
        )
        |> Some

let internal overlayExtension
    extensionName
    (inherited: AST.ExtensionBlock)
    (declared: AST.ExtensionBlock) =
    { AST.ExtensionBlock.Image = declared.Image |> Option.orElse inherited.Image
      AST.ExtensionBlock.Platform = declared.Platform |> Option.orElse inherited.Platform
      AST.ExtensionBlock.Variables = addExtensionVariables inherited.Variables declared.Variables
      AST.ExtensionBlock.Script = declared.Script |> Option.orElse inherited.Script
      AST.ExtensionBlock.Cpus = declared.Cpus |> Option.orElse inherited.Cpus
      AST.ExtensionBlock.Defaults =
        addExtensionEntries extensionName "defaults" inherited.Defaults declared.Defaults
      AST.ExtensionBlock.Env =
        addExtensionEntries extensionName "env" inherited.Env declared.Env }

let private overlayExtensions inherited declared =
    declared
    |> Map.fold (fun extensions name declaredExtension ->
        let extension =
            inherited
            |> Map.tryFind name
            |> Option.map (fun inheritedExtension ->
                overlayExtension name inheritedExtension declaredExtension)
            |> Option.defaultValue declaredExtension

        extensions |> Map.add name extension
    ) inherited

// this is the first stage: load project and get dependencies references
let private loadProjectDef
    (options: ConfigOptions.Options)
    (workspaceConfig: AST.Workspace.WorkspaceFile)
    (scriptDeniedPathGlobs: string list)
    evaluationContext
    extensions
    scripts
    projectId =
    let projectDir = FS.combinePath options.Workspace projectId
    let projectFile = FS.combinePath projectDir "PROJECT"

    Log.Debug("Loading project definition '{ProjectId}'", projectId)

    let projectConfig =
        match projectFile with
        | FS.File projectFile ->
            let projectContent = File.ReadAllText projectFile
            Terrabuild.Configuration.FrontEnd.Project.parseWithSource projectFile projectContent
        | _ ->
            raiseInvalidArg $"No PROJECT found in directory '{projectFile}'"

    let phaseNames = workspaceConfig.Phases |> Map.keys |> Set.ofSeq
    projectConfig.Targets
    |> Map.iter (fun _ target -> target.Phase |> Option.iter (resolvePhaseReference phaseNames >> ignore))

    let extensions = overlayExtensions extensions projectConfig.Extensions

    let projectScriptOverrides =
        projectConfig.Extensions
        |> Map.choose (fun extensionName ext ->
            match ext.Script with
            | None -> None
            | Some scriptExpression ->
                let script =
                    scriptExpression
                    |> Eval.eval evaluationContext
                    |> Eval.asStringOption
                    |> Option.map (fun script ->
                        if isHttpScriptUrl script then script
                        else script |> FS.workspaceRelative options.Workspace projectDir)
                validateExtensionScriptOverride extensionName script
                Some script)

    let scripts =
        projectScriptOverrides
        |> Map.fold (fun scripts extensionName script ->
            match script with
            | Some script ->
                let loader =
                    Extensions.lazyLoadScript
                        options.Workspace
                        scriptDeniedPathGlobs
                        extensionName
                        (Some script)
                scripts |> Map.add extensionName loader
            | None ->
                scripts |> Map.remove extensionName
        ) scripts

    let evalAsStringSet expr =
        expr
        |> Option.bind (Eval.asStringSetOption << Eval.eval evaluationContext)
        |> Option.defaultValue Set.empty

    let parseContext = 
        let context = { Terrabuild.ScriptingContracts.ExtensionContext.Debug = options.Debug
                        Terrabuild.ScriptingContracts.ExtensionContext.Directory = projectDir
                        Terrabuild.ScriptingContracts.ExtensionContext.CI = options.Run.IsSome }
        Value.Map (Map [ "context", Value.Object context ])

    let declaredProjectType = projectConfig.Project.Type

    let projectTypeDefaults, projectId, projectType =
        match declaredProjectType with
        | None -> ProjectInfo.Default, projectId |> String.toLower, SCOPE_PATH
        | Some projectType ->
            let result =
                Extensions.getScript projectType scripts
                |> Extensions.invokeScriptDefault<ProjectInfo> parseContext
            let defaults =
                match result with
                | Extensions.Success result -> result
                | Extensions.ScriptNotFound -> raiseSymbolError $"Script {projectType} was not found"
                | Extensions.TargetNotFound -> ProjectInfo.Default
                | Extensions.ErrorTarget exn -> forwardExternalError($"Invocation failure of default metadata for extension '{projectType}'", exn)
            match defaults.Id with
            | Some canonicalId -> defaults, canonicalId, $"{projectType}"
            | _ -> defaults, projectId |> String.toLower, SCOPE_PATH

    let initializersForDefaults =
        match declaredProjectType with
        | Some projectType -> projectConfig.Project.Initializers |> Set.remove projectType
        | None -> projectConfig.Project.Initializers

    let initProjectInfo =
        initializersForDefaults |> Set.fold (fun projectInfo init ->
            let result =
                Extensions.getScript init scripts
                |> Extensions.invokeScriptDefault<ProjectInfo> parseContext

            let initProjectInfo =
                match result with
                | Extensions.Success result -> result
                | Extensions.ScriptNotFound -> raiseSymbolError $"Script {init} was not found"
                | Extensions.TargetNotFound -> ProjectInfo.Default // NOTE: if no default metadata is exported - this will silently use default configuration, probably emit warning
                | Extensions.ErrorTarget exn -> forwardExternalError($"Invocation failure of default metadata for extension '{init}'", exn)

            { projectInfo with
                ProjectInfo.Outputs = projectInfo.Outputs + initProjectInfo.Outputs
                ProjectInfo.Dependencies = projectInfo.Dependencies + initProjectInfo.Dependencies }) ProjectInfo.Default

    let defaultsProjectInfo =
        { ProjectInfo.Default with
            ProjectInfo.DependencyResolution =
                projectTypeDefaults.DependencyResolution
                |> Option.orElse initProjectInfo.DependencyResolution
            ProjectInfo.Outputs = projectTypeDefaults.Outputs + initProjectInfo.Outputs
            ProjectInfo.Dependencies = projectTypeDefaults.Dependencies + initProjectInfo.Dependencies }

    let usedExtensions =
        let projectType =
            declaredProjectType
            |> Option.map Set.singleton
            |> Option.defaultValue Set.empty

        let targetExtensions =
            projectConfig.Targets
            |> Seq.collect (fun (KeyValue(_, target)) -> target.Steps |> Seq.map _.Extension)
            |> Set.ofSeq

        projectType
        |> Set.union projectConfig.Project.Initializers
        |> Set.union targetExtensions

    let usedExtensionProjectReferences =
        usedExtensions
        |> Seq.choose (fun extensionName -> extensions |> Map.tryFind extensionName)
        |> Seq.toList
        |> fun usedExtensions ->
            (Dependencies.reflectionFind usedExtensions)
            |> Set.union (Dependencies.reflectionFindProjectReferences usedExtensions |> Set.map (fun dep -> $"project.{dep}"))

    let dependsOn =
        // collect dependencies for all the project
        // NOTE we are keeping only project dependencies as we want to construct project graph
        projectConfig.Project.DependsOn |> Option.defaultValue Set.empty
        |> Set.union (Dependencies.reflectionFind projectConfig)
        |> Set.union (projectConfig |> Dependencies.reflectionFindProjectReferences |> Set.map (fun dep -> $"project.{dep}"))
        |> Set.union usedExtensionProjectReferences
        |> Set.choose (fun dep ->
            match dep with
            | String.Regex "^project\.(.+)$" [ dependencyId ] ->
                match projectConfig.Project.Name with
                | Some currentProjectId when String.Equals(currentProjectId, dependencyId, StringComparison.OrdinalIgnoreCase) -> None
                | _ -> Some dependencyId
            | _ -> None)
        |> Set.map (fun depId -> format_project_id SCOPE_NAME depId)

    let labels = projectConfig.Project.Labels
    let initializers = projectConfig.Project.Initializers

    let projectTargets =
        // apply target override
        let buildProjectTargets() =
            projectConfig.Targets |> Map.map (fun targetName targetBlock ->
                // apply workspace default value
                let workspaceTarget = workspaceConfig.Targets |> Map.tryFind targetName
                let build = targetBlock.Build |> Option.orElseWith (fun () -> workspaceTarget |> Option.bind _.Build)
                let dependsOn = targetBlock.DependsOn |> Option.orElseWith (fun () -> workspaceTarget |> Option.bind _.DependsOn)
                let cache = targetBlock.Cache |> Option.orElseWith (fun () -> workspaceTarget |> Option.bind _.Cache)
                let group = targetBlock.Batch |> Option.orElseWith (fun () -> workspaceTarget |> Option.bind _.Batch)
                let phase = targetBlock.Phase |> Option.orElseWith (fun () -> workspaceTarget |> Option.bind _.Phase)
                let targetLock = targetBlock.Lock |> Option.orElseWith (fun () -> workspaceTarget |> Option.bind _.Lock)
                let outputs = targetBlock.Outputs |> Option.orElseWith (fun () -> workspaceTarget |> Option.bind _.Outputs)
                let environmentSensitive = targetBlock.EnvironmentSensitive |> Option.orElseWith (fun () -> workspaceTarget |> Option.bind _.EnvironmentSensitive)
                { targetBlock with 
                    Build = build
                    DependsOn = dependsOn
                    Cache = cache
                    Batch = group
                    Phase = phase
                    Lock = targetLock
                    Outputs = outputs
                    EnvironmentSensitive = environmentSensitive })
        let environments =
            projectConfig.Project.Environments
            |> Option.bind (Eval.asStringSetOption << Eval.eval evaluationContext)
        let isProjectEnabledForEnvironment =
            match options.Environment, environments with
            | Some environment, Some environments ->
                let matcher = Matcher()
                matcher.AddIncludePatterns(environments |> Seq.map String.toLower)
                matcher.Match([environment |> String.toLower]).HasMatches
            | _ -> true
        if isProjectEnabledForEnvironment then
            Log.Debug("Enabling project '{ProjectId}'", projectId)
            buildProjectTargets()
        else
            Log.Debug("Disabling project '{ProjectId}'", projectId)
            Map.empty

    // convert relative dependencies to absolute dependencies respective to workspaceDirectory
    let projectDependencies =
        defaultsProjectInfo.Dependencies
        |> Set.map (fun dep ->
            match resolve_dependency_scope defaultsProjectInfo, declaredProjectType with
            | DependencyResolution.Scope, Some extensionScope -> format_project_id extensionScope dep
            | _ ->
                let relativeWks = FS.workspaceRelative options.Workspace projectDir dep |> String.toLower
                format_project_id SCOPE_PATH relativeWks)

    // NOTE: we add scripts as dependencies so they are part of the hash
    let projectIncludes =
        projectScriptOverrides
        |> Seq.choose (fun (KeyValue(_, script)) -> script)
        |> Set.ofSeq
        |> Set.union (projectConfig.Project.Includes |> evalAsStringSet)

    let projectIgnores = projectConfig.Project.Ignores |> evalAsStringSet

    let projectOutputs = projectConfig.Project.Outputs |> evalAsStringSet |> Set.union defaultsProjectInfo.Outputs

    // enrich workspace locals with project locals
    // NOTE we are checking for duplicated fields as this is an error
    let locals =
        workspaceConfig.Locals |> Map.iter (fun name _ ->
            if projectConfig.Locals |> Map.containsKey name then raiseParseError $"duplicated local '{name}'")
        workspaceConfig.Locals |> Map.addMap projectConfig.Locals

    { LoadedProject.Id = projectId
      LoadedProject.Type = projectType
      LoadedProject.Name = projectConfig.Project.Name
      LoadedProject.DependsOn = dependsOn
      LoadedProject.Dependencies = projectDependencies
      LoadedProject.Includes = projectIncludes
      LoadedProject.Ignores = projectIgnores
      LoadedProject.Outputs = projectOutputs
      LoadedProject.Targets = projectTargets
      LoadedProject.Labels = labels
      LoadedProject.Initializers = initializers
      LoadedProject.Extensions = extensions
      LoadedProject.Scripts = scripts
      LoadedProject.Locals = locals }


let internal buildLocalPlan knownNames (locals: Map<string, Expr>) =
    let pending =
        locals
        |> Map.toSeq
        |> Seq.map (fun (name, expr) ->
            let localName = $"local.{name}"
            localName, (expr, Dependencies.find expr))
        |> Map.ofSeq

    let rec build available pending plan =
        match pending |> Seq.tryFind (fun (KeyValue(_, (_, dependencies))) -> Set.isSubset dependencies available) with
        | Some (KeyValue(localName, (expr, _))) ->
            build (available |> Set.add localName) (pending |> Map.remove localName) ((localName, expr) :: plan)
        | None when pending.IsEmpty -> Ok (plan |> List.rev)
        | None ->
            let (KeyValue(localName, (_, dependencies))) = pending |> Seq.head
            Error (localName, dependencies - available)

    build knownNames pending []


// this is the final stage: create targets and create the project
let private finalizeProject repository workspaceDir projectDir evaluationContext phaseNames (projectDef: LoadedProject) (projectDependencies: Map<string, Project>) =
    let startFinalize = DateTime.UtcNow
    let projectId = projectDef.Id

    // get dependencies on files
    let visibleFiles = Git.enumeratedCommittedFiles workspaceDir projectDir |> Set.ofList
    let additionalFiles =
        projectDir
        |> IO.enumerateFilesBut projectDef.Includes (projectDef.Outputs + projectDef.Ignores)
        |> Set
    let files = visibleFiles + additionalFiles

    let filesHash = files |> Hash.sha256files
    let fileNameHash = files |> Set.map (fun file -> FS.relativePath projectDir file) |> Hash.sha256strings

    let dependenciesHash =
        let versionDependencies = projectDependencies |> Map.map (fun _ depProj -> depProj.Hash)
        versionDependencies.Values
        |> Seq.sort
        |> Hash.sha256strings

    // NOTE: this is the hash (modulo target name) used for reconcialiation across executions
    let projectHash = [ repository; projectId; filesHash; fileNameHash; dependenciesHash ] |> Hash.sha256strings

    let evaluationContext = 
        let terrabuildProjectVars =
            Map [ if projectDef.Name.IsSome then "terrabuild.project", Value.String projectDef.Name.Value
                  "terrabuild.project_slug", projectDir |> String.slugify |> Value.String 
                  "terrabuild.version", Value.String projectHash ]
  
        let projectsMap =
            seq {
                if projectDef.Name.IsSome then
                    yield projectDef.Name.Value, Value.Map (Map ["version", Value.String projectHash])

                for KeyValue(_, project) in projectDependencies do
                    match project.Name with
                    | Some id -> yield id, Value.Map (Map ["version", Value.String project.Hash])
                    | None -> ()
            }
            |> Map.ofSeq

        let projectAliases =
            projectsMap
            |> Seq.map (fun (KeyValue(projectName, value)) -> $"project.{projectName}", value)
            |> Map.ofSeq

        { evaluationContext with
            Eval.Data =
                evaluationContext.Data
                |> Map.addMap terrabuildProjectVars
                |> Map.add "project" (Value.Map projectsMap)
                |> Map.addMap projectAliases }

    let localPlan =
        lazy (
            let knownNames =
                evaluationContext.Data.Keys
                |> Set.ofSeq
                |> Set.add "terrabuild.target"
                |> Set.add "terrabuild.phase"
            buildLocalPlan knownNames projectDef.Locals)

    let projectSteps =
        projectDef.Targets |> Map.map (fun targetName target ->
            let targetPhase =
                target.Phase |> Option.bind (resolvePhaseReference phaseNames)

            let evaluationContext =
                let mutable evaluationContext =
                    let terrabuildTargetVars =
                        Map [ "terrabuild.target", Value.String targetName
                              "terrabuild.phase", targetPhase |> Option.map Value.String |> Option.defaultValue Value.Nothing ]

                    { evaluationContext with
                        Eval.ProjectDir = Some projectDir
                        Eval.Data =
                            evaluationContext.Data
                            |> Map.addMap terrabuildTargetVars }

                match localPlan.Value with
                | Ok locals ->
                    try
                        for localName, localExpr in locals do
                            try
                                let localValue = Eval.eval evaluationContext localExpr
                                evaluationContext <- { evaluationContext with Data = evaluationContext.Data |> Map.add localName localValue }
                            with exn ->
                                forwardExternalError($"Failed to evaluate '{localName}'", exn)
                        evaluationContext
                    with exn ->
                        forwardExternalError("Failed to evaluate locals", exn)
                | Error (subscription, signals) ->
                    let unraisedSignals = signals |> String.join ","
                    raiseInvalidArg $"Failed to evaluate '{subscription}': local value '{unraisedSignals}' is not declared."

            // use value from project target
            // otherwise use workspace target
            // defaults to allow caching
            let targetBuild =
                match target.Build with
                | None -> None
                | Some targetBuild ->
                    let targetBuild = targetBuild |> Eval.eval evaluationContext |> Eval.asEnum
                    match targetBuild with
                    | Ok "lazy" -> Some BuildMode.Lazy
                    | Ok "auto" -> Some BuildMode.Auto
                    | Ok "always" -> Some BuildMode.Always
                    | Ok x -> raiseParseError $"Invalid build value '{x}'"
                    | Error error -> raiseParseError error

            let targetSteps =
                target.Steps |> List.fold (fun (targetSteps) step ->
                    let extensionName = step.Extension
                    let extension = 
                        match projectDef.Extensions |> Map.tryFind extensionName with
                        | Some extension -> extension
                        | _ -> raiseSymbolError $"Extension {step.Extension} is not defined"

                    let context =
                        extension.Defaults |> Option.defaultValue Map.empty
                        |> Map.addMap step.Parameters
                        |> Expr.Map
                        |> Eval.eval evaluationContext

                    let image =
                        match extension.Image with
                        | Some container ->
                            match Eval.eval evaluationContext container with
                            | Value.String container -> Some container
                            | Value.Nothing -> None
                            | _ -> raiseTypeError "container must evaluate to a string"
                        | _ -> None

                    let platform =
                        match extension.Platform with
                        | Some platform ->
                            match Eval.eval evaluationContext platform with
                            | Value.String platform -> Some platform
                            | Value.Nothing -> None
                            | _ -> raiseTypeError "container must evaluate to a string"
                        | _ -> None

                    let cpus =
                        match extension.Cpus with
                        | Some cpus ->
                            match Eval.eval evaluationContext cpus with
                            | Value.Number cpus ->
                                if cpus < 1 then raiseTypeError "cpus must evaluate to a strictly positive number"
                                Some cpus
                            | Value.Nothing -> None
                            | _ -> raiseTypeError "cpus must evaluate to a number"
                        | _ -> None

                    let script =
                        match Extensions.getScript extensionName projectDef.Scripts with
                        | Some script -> script
                        | _ -> raiseSymbolError $"Extension {step.Extension} is not defined"

                    let variables =
                        extension.Variables
                        |> Option.bind (Eval.asStringSetOption << Eval.eval evaluationContext)
                        |> Option.defaultValue Set.empty

                    let envs =
                        extension.Env
                        |> Option.map (Map.map (fun _ -> Eval.valueToString << Eval.eval evaluationContext))
                        |> Option.defaultValue Map.empty

                    let targetStepHash =
                        buildTargetStepHash extensionName step.Command image platform cpus variables script.Identity

                    let targetContext = {
                        TargetStep.Hash = targetStepHash
                        TargetStep.Image = image
                        TargetStep.Platform = platform
                        TargetStep.Cpus = cpus
                        TargetStep.ContainerVariables = variables
                        TargetStep.Envs = envs
                        TargetStep.Extension = extensionName
                        TargetStep.Command = step.Command
                        TargetStep.Script = script
                        TargetStep.Context = context
                    }

                    let steps = targetSteps @ [ targetContext ]
                    steps
                ) []

            let targetDependsOn = target.DependsOn |> Option.defaultValue Set.empty

            let targetOutputs =
                let targetOutputs =
                    target.Outputs
                    |> Option.bind (Eval.asStringSetOption << Eval.eval evaluationContext)
                match targetOutputs with
                | Some outputs -> outputs
                | _ -> projectDef.Outputs

            let targetCache =
                match target.Cache with
                | None -> None
                | Some targetCache ->
                    let targetCache = targetCache |> Eval.eval evaluationContext |> Eval.asEnum
                    match targetCache with
                    | Ok "none" -> Some ArtifactMode.None
                    | Ok "workspace" -> Some ArtifactMode.Workspace
                    | Ok "managed" -> Some ArtifactMode.Managed
                    | Ok "external" -> Some ArtifactMode.External
                    | Ok x -> raiseParseError $"Invalid artifacts value '{x}'"
                    | Error error -> raiseParseError error

            let targetBatch = 
                let targetGroup =
                    target.Batch
                    |> Option.map (fun batch -> batch |> Eval.eval evaluationContext |> Eval.asEnum)
                match targetGroup with
                | Some group ->
                    match group with
                    | Ok "never" -> BatchMode.Never
                    | Ok "partition" -> BatchMode.Partition
                    | Ok "single" -> BatchMode.Single
                    | Ok x -> raiseParseError $"Invalid group value '{x}'"
                    | Error error -> raiseParseError error
                | _ -> BatchMode.Single

            let environmentSensitive =
                target.EnvironmentSensitive
                |> Option.map (fun value ->
                    match value |> Eval.eval evaluationContext with
                    | Value.Bool value -> value
                    | _ -> raiseTypeError "environment_sensitive must evaluate to a boolean")

            let targetLock =
                target.Lock
                |> Option.bind (fun value ->
                    match value |> Eval.eval evaluationContext with
                    | Value.String value when String.IsNullOrWhiteSpace(value) ->
                        raiseInvalidArg "lock must not be empty"
                    | Value.String value -> Some value
                    | Value.Nothing -> None
                    | _ -> raiseTypeError "lock must evaluate to a string or nothing")

            let evaluationInputs =
                buildEvaluationInputs evaluationContext projectDef.Locals target projectDef.Extensions

            let clusterHash =
                targetSteps
                |> List.map (fun step -> step.Hash)
                |> Hash.sha256strings

            let target =
                { Target.Hash = buildDeclaredTargetHash target
                  Target.ClusterHash = clusterHash
                  Target.Build = targetBuild
                  Target.Batch = targetBatch
                  Target.Phase = targetPhase
                  Target.Lock = targetLock
                  Target.DependsOn = targetDependsOn
                  Target.Cache = targetCache
                  Target.Outputs = targetOutputs
                  Target.EvaluationInputs = evaluationInputs
                  Target.EnvironmentSensitive = environmentSensitive
                  Target.Steps = targetSteps }

            target
        )

    let relativeFiles = files |> Set.map (FS.relativePath projectDir)

    let projectDependencies = projectDependencies.Keys |> Set.ofSeq

    let endFinalize = DateTime.UtcNow
    let projectId = format_project_id projectDef.Type projectDef.Id
    DiagnosticsTelemetry.recordProject projectId ((endFinalize - startFinalize).TotalMilliseconds)

    { Project.Id = projectId
      Project.Name = projectDef.Name
      Project.Directory = projectDir
      Project.Hash = projectHash
      Project.Dependencies = projectDependencies
      Project.Files = relativeFiles
      Project.Targets = projectSteps
      Project.Labels = projectDef.Labels
      Project.Types = projectDef.Initializers }



let private validatePhases (phases: Map<string, AST.Workspace.PhaseBlock>) =
    phases
    |> Map.iter (fun phaseName phase ->
        phase.DependsOn
        |> Set.iter (fun dependency ->
            if phases.ContainsKey dependency |> not then
                raiseSymbolError $"Phase '{phaseName}' depends on undefined phase '{dependency}'"))

    let mutable visited = Set.empty<string>
    let rec visit path phaseName =
        if path |> List.contains phaseName then
            let cycle =
                phaseName :: path
                |> List.rev
                |> String.join " -> "
            raiseInvalidArg $"Circular phase dependency detected: {cycle}"
        elif visited |> Set.contains phaseName |> not then
            let path = phaseName :: path
            phases[phaseName].DependsOn |> Set.iter (visit path)
            visited <- visited |> Set.add phaseName

    phases |> Map.keys |> Seq.iter (visit [])

let read (options: ConfigOptions.Options) =
    $"{Ansi.Emojis.unicorn} Settings" |> Terminal.writeLine

    // Restore transactions may contain the last complete copy of project files.
    // Recover them before configuration, file hashing, or operation resolution reads the workspace.
    Cache.recoverWorkspaceOutputTransactions options.Workspace

    let workspaceContent = FS.combinePath options.Workspace "WORKSPACE" |> File.ReadAllText
    let workspaceConfig =
        try
            FrontEnd.Workspace.parseWithSource (FS.combinePath options.Workspace "WORKSPACE") workspaceContent
        with exn ->
            forwardParseError("Failed to read WORKSPACE configuration file", exn)

    validatePhases workspaceConfig.Phases
    let phaseNames = workspaceConfig.Phases |> Map.keys |> Set.ofSeq
    workspaceConfig.Targets
    |> Map.iter (fun _ target -> target.Phase |> Option.iter (resolvePhaseReference phaseNames >> ignore))

    let engine =
        match workspaceConfig.Workspace.Engine with
        | None -> options.Engine
        | Some "docker" -> ConfigOptions.Engine.Docker
        | Some "podman" -> ConfigOptions.Engine.Podman
        | Some "host" -> ConfigOptions.Engine.Host
        | Some x -> raiseInvalidArg $"Invalid engine option '{x}'"

    let options =
        { options with
            Engine = engine
            Configuration = options.Configuration |> Option.orElse workspaceConfig.Workspace.Configuration
            Environment = options.Environment |> Option.orElse workspaceConfig.Workspace.Environment }

    let configInfos =
        let targets = options.Targets |> String.join " "
        let labels = options.Labels |> Option.map (fun labels -> labels |> String.join " ")
        let types = options.Types |> Option.map (fun types -> types |> String.join " ")
        let projects = options.Projects |> Option.map (fun projects -> projects |> String.join " ")
        let warningConfig = [
            if options.Force then "force"
            elif options.Retry then "retry"
            if options.DryRun then "dry-run" ] |> String.join(" ")
        [
            if warningConfig |> String.IsNullOrWhiteSpace |> not then $"Build flags [{warningConfig}]"
            $"Engine {options.Engine |> string |> String.toLower}"
            if options.Run.IsSome then $"Source control {options.Run.Value.Name}"
            if options.Configuration.IsSome then $"Configuration {options.Configuration.Value}"
            if options.Environment.IsSome then $"Environment {options.Environment.Value}"
            $"Targets [{targets}]"
            if labels.IsSome then $"Labels [{labels.Value}]"
            if types.IsSome then $"Types [{types.Value}]"
            if projects.IsSome then $"Projects [{projects.Value}]"
        ]
    configInfos |> List.iter (fun configInfo -> $" {Ansi.Styles.green}{Ansi.Emojis.arrow}{Ansi.Styles.reset} {configInfo}" |> Terminal.writeLine)

#if RELEASE
    // check min version requirement
    match workspaceConfig.Workspace.Version with
    | Some minVersion ->
        let actualVersion = Version.version()
        if actualVersion |> Version.isAtLeast minVersion |> not then
            raiseInvalidArg $"Workspace requires version '{minVersion}' or newer (found '{actualVersion}')."
    | _ -> ()
#endif

    $"{Ansi.Emojis.bolt} Building graph" |> Terminal.writeLine

    let evaluationContext = buildEvaluationContext engine options workspaceConfig
    // Repository identity is normalized at the source-control boundary so equivalent
    // local and CI remotes resolve to the same repository namespace for hashing.
    let repository =
        options.Repository
        |> Git.tryNormalizeRepositoryIdentity
        |> Option.defaultValue options.Repository

    let scriptDeniedPathGlobs =
        workspaceConfig.Workspace.Deny
        |> Option.map Set.toList
        |> Option.defaultValue default_script_deny_globs

    let scripts = buildScripts options workspaceConfig scriptDeniedPathGlobs evaluationContext

    let extensions = Extensions.SystemExtensions |> Map.addMap workspaceConfig.Extensions

    let searchProjectsAndApply() =
        let workspaceIgnores = workspaceConfig.Workspace.Ignores |> Option.defaultValue default_ignores
        let scanFolder = scanFolders options.Workspace workspaceIgnores
        let projectLoading = ConcurrentDictionary<string, bool>()
        let projectIds = ConcurrentDictionary<string, string>()
        let projects = ConcurrentDictionary<string, Project>()
        use hub = Hub.Create(options.MaxConcurrency)

        let rec loadProject projectDir =
            if projectLoading.TryAdd(projectDir, true) then

                // parallel load of projects
                hub.SubscribeBackground projectDir [] (fun () ->
                    let loadedProject =
                        try
                            // load project and force loading all dependencies as well
                            let loadedProject =
                                loadProjectDef
                                    options
                                    workspaceConfig
                                    scriptDeniedPathGlobs
                                    evaluationContext
                                    extensions
                                    scripts
                                    projectDir
                            match loadedProject.Name with
                            | Some projectId ->
                                if projectIds.TryAdd(projectId, projectDir) |> not then
                                    raiseSymbolError $"Project id '{projectId}' is already defined in project '{projectIds[projectId]}'"
                            | _ -> ()

                            loadedProject
                        with exn ->
                            forwardParseError($"Failed to read PROJECT configuration '{projectDir}'", exn)

                    // await dependencies to be loaded
                    let projectPathSignals =
                        loadedProject.Dependencies
                        |> Seq.map (fun depId -> hub.GetSignal<Project> depId)
                        |> List.ofSeq

                    let dependsOnSignals =
                        loadedProject.DependsOn
                        |> Seq.map (fun depId -> hub.GetSignal<Project> depId)
                        |> List.ofSeq

                    let awaitedSignals = projectPathSignals @ dependsOnSignals
                    hub.SubscribeBackground projectDir awaitedSignals (fun () ->
                        try
                            // build task & code & notify
                            let dependsOnProjects = 
                                awaitedSignals
                                |> Seq.map (fun projectDependency ->
                                    let project = projectDependency.Get<Project>()
                                    project.Id, project)
                                |> Map.ofSeq

                            let phaseNames = workspaceConfig.Phases |> Map.keys |> Set.ofSeq
                            let project = finalizeProject repository options.Workspace projectDir evaluationContext phaseNames loadedProject dependsOnProjects
                            if projects.TryAdd(project.Id, project) |> not then raiseBugError "Unexpected error"

                            // signal canonical id
                            let loadedProjectPathIdSignal = hub.GetSignal<Project> project.Id
                            loadedProjectPathIdSignal.Set(project)

                            match loadedProject.Name with
                            | Some projectId ->
                                let loadedProjectIdSignal = hub.GetSignal<Project> (format_project_id SCOPE_NAME projectId)
                                loadedProjectIdSignal.Set(project)
                            | _ -> ()
                        with exn -> forwardExternalError($"Error while parsing project '{projectDir}'", exn)))

        scanProjectDirectories options.MaxConcurrency options.Workspace scanFolder (fun projectDir ->
            let projectFile = projectDir |> FS.relativePath options.Workspace
            try
                loadProject projectFile
            with exn ->
                forwardExternalError($"Error while parsing project '{projectFile}'", exn))
        let status = hub.WaitCompletion()

        match status with
        | Status.Ok ->
            Log.Debug("Configuration successful")
            projects |> Map.ofDict
        | Status.UnfulfilledSubscription (subscription, signals) ->
            let unraisedSignals = signals |> String.join ","
            Log.Fatal("Configuration '{Subscription}' has pending operations on '{UnraisedSignals}'", subscription, unraisedSignals)
            raiseInvalidArg $"Project '{subscription}' has pending operations on '{unraisedSignals}'. Check for circular dependencies."
        | Status.SubscriptionError edi ->
            Log.Fatal(edi.SourceException, "Configuration failed with exception")
            forwardExternalError("Configuration failed", edi.SourceException)


    let projects = searchProjectsAndApply()

    // select dependencies with labels if any
    let projectSelection =
        match options.Labels with
        | Some filter -> projects |> Map.filter (fun _ config -> Set.intersect config.Labels filter <> Set.empty)
        | _ -> projects

    // select dependencies with project types if any
    let projectSelection =
        match options.Types with
        | Some filter -> projectSelection |> Map.filter (fun _ config -> Set.intersect config.Types filter <> Set.empty)
        | _ -> projectSelection

    // Select by declared identifier, internal id, or workspace-relative path. Unnamed projects use
    // their path as their identifier, so restricting this filter to Project.Name made them
    // impossible to select and silently produced an empty graph.
    let projectSelection =
        match options.Projects with
        | Some filter ->
            let filter = filter |> Set.map normalizeProjectSelector
            let matchedSelectors =
                projects
                |> Map.values
                |> Seq.collect projectSelectors
                |> Set.ofSeq
                |> Set.intersect filter
            let invalidSelectors = filter - matchedSelectors
            if invalidSelectors.IsEmpty |> not then
                let invalid = invalidSelectors |> String.join ", "
                raiseInvalidArg $"Unknown project selector(s): {invalid}. Use a project identifier or workspace-relative path."

            projectSelection
            |> Map.filter (fun _ config -> Set.intersect (projectSelectors config) filter <> Set.empty)
        | _ -> projectSelection

    let selectedProjects = projectSelection |> Map.keys |> Set

    let workspaceId = workspaceConfig.Workspace.Id

    let targets =
        workspaceConfig.Targets
        |> Map.map (fun _ target -> target.DependsOn |> Option.defaultValue Set.empty)

    let workspaceConfig =
        { Workspace.Id = workspaceId
          Workspace.SelectedProjects = selectedProjects
          Workspace.Projects = projects |> Map.ofDict
          Workspace.Targets = targets
          Workspace.Phases = workspaceConfig.Phases |> Map.map (fun _ phase -> phase.DependsOn) }
    options, workspaceConfig
