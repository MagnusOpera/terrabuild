module Configuration
open System.IO
open Collections
open System
open System.Collections.Concurrent
open Terrabuild.Extensibility
open Terrabuild.Expressions
open Errors
open Terrabuild.PubSub
open Microsoft.Extensions.FileSystemGlobbing
open Serilog
open Terrabuild.Configuration
open System.Runtime.InteropServices
open GraphDef

[<RequireQualifiedAccess>]
type TargetOperation = {
    Hash: string
    Image: string option
    Platform: string option
    ContainerVariables: string set
    Extension: string
    Command: string
    Script: Terrabuild.Scripting.Script
    Context: Value
}

[<RequireQualifiedAccess>]
type Target = {
    Hash: string
    Build: Build option
    Batch: bool
    DependsOn: string set
    Outputs: string set
    Cache: Artifacts option
    Operations: TargetOperation list
}


[<RequireQualifiedAccess>]
type Project = {
    Id: string option
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

    // All discovered projects in workspace
    Projects: Map<string, Project>
}

type private LazyScript = Lazy<Terrabuild.Scripting.Script>

[<RequireQualifiedAccess>]
type private LoadedProject = {
    Id: string option
    DependsOn: string set
    Dependencies: string set
    Includes: string set
    Ignores: string set
    Outputs: string set
    Targets: Map<string, AST.Project.TargetBlock>
    Labels: string set
    Types: string set
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
        if matcher.Match(relativeDir).HasMatches then
            match FS.combinePath dir "WORKSPACE" with
            | FS.File _ -> false
            | _ -> true
        else
            false


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

let private buildEvaluationContext engine (options: ConfigOptions.Options) (workspaceConfig: AST.Workspace.WorkspaceFile) =
    let tagValue = 
        match options.Label with
        | Some tag -> Value.String tag
        | _ -> Value.Nothing

    let noteValue =
        match options.Note with
        | Some note -> Value.String note
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

        let engine =
            match engine with
            | Some engine -> Value.Enum engine
            | None -> Value.Enum "none"

        Map [ "terrabuild.configuration", configValue
              "terrabuild.environment", envValue
              "terrabuild.branch_or_tag", Value.String options.BranchOrTag 
              "terrabuild.head_commit", Value.String options.HeadCommit.Sha
              "terrabuild.retry", Value.Bool options.Retry 
              "terrabuild.force", Value.Bool options.Force 
              "terrabuild.ci", Value.Bool options.Run.IsSome
              "terrabuild.engine", engine
              "terrabuild.debug", Value.Bool options.Debug 
              "terrabuild.tag", tagValue 
              "terrabuild.note", noteValue
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
                    if deps <> Set.empty then raiseInvalidArg "Default value for variable '{name}' must have no dependencies"
                    expr |> Eval.eval evaluationContext |> Some

            let value =
                match $"TB_VAR_{name}" |> Environment.getEnvVar with
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


let private buildScripts (options: ConfigOptions.Options) (workspaceConfig: AST.Workspace.WorkspaceFile) evaluationContext =
    // load system extensions
    let sysScripts =
        Extensions.systemExtensions
        |> Map.map (fun _ _ -> None)
        |> Map.map Extensions.lazyLoadScript

    // load user extension
    let userScripts =
        workspaceConfig.Extensions
        |> Map.map (fun _ ext ->
            let script =
                ext.Script
                |> Option.bind (Eval.asStringOption << Eval.eval evaluationContext)
            match script with
            | Some script -> script |> FS.workspaceRelative options.Workspace "" |> Some
            | _ -> None)
        |> Map.map Extensions.lazyLoadScript

    let scripts = sysScripts |> Map.addMap userScripts
    scripts





// this is the first stage: load project and get dependencies references
let private loadProjectDef (options: ConfigOptions.Options) (workspaceConfig: AST.Workspace.WorkspaceFile) evaluationContext extensions scripts projectId =
    let projectDir = FS.combinePath options.Workspace projectId
    let projectFile = FS.combinePath projectDir "PROJECT"

    Log.Debug("Loading project definition {ProjectId}", projectId)

    let projectConfig =
        match projectFile with
        | FS.File projectFile ->
            let projectContent = File.ReadAllText projectFile
            Terrabuild.Configuration.FrontEnd.Project.parse projectContent
        | _ ->
            raiseInvalidArg $"No PROJECT found in directory '{projectFile}'"

    let extensions = extensions |> Map.addMap projectConfig.Extensions

    let projectScripts =
        projectConfig.Extensions |> Map.map (fun _ ext ->
            ext.Script
            |> Option.bind (Eval.asStringOption << Eval.eval evaluationContext)
            |> Option.map (FS.workspaceRelative options.Workspace projectDir))

    let scripts =
        scripts
        |> Map.addMap (projectScripts |> Map.map Extensions.lazyLoadScript)

    let evalAsStringSet expr =
        expr
        |> Option.bind (Eval.asStringSetOption << Eval.eval evaluationContext)
        |> Option.defaultValue Set.empty

    let initProjectInfo =
        projectConfig.Project.Initializers |> Set.fold (fun projectInfo init ->
            let parseContext = 
                let context = { Terrabuild.Extensibility.ExtensionContext.Debug = options.Debug
                                Terrabuild.Extensibility.ExtensionContext.Directory = projectDir
                                Terrabuild.Extensibility.ExtensionContext.CI = options.Run.IsSome }
                Value.Map (Map [ "context", Value.Object context ])

            let result =
                Extensions.getScript init scripts
                |> Extensions.invokeScriptMethod<ProjectInfo> "__defaults__" parseContext

            let initProjectInfo =
                match result with
                | Extensions.Success result -> result
                | Extensions.ScriptNotFound -> raiseSymbolError $"Script {init} was not found"
                | Extensions.TargetNotFound -> ProjectInfo.Default // NOTE: if __defaults__ is not found - this will silently use default configuration, probably emit warning
                | Extensions.ErrorTarget exn -> forwardExternalError($"Invocation failure of command '__defaults__' for extension '{init}'", exn)

            { projectInfo with
                ProjectInfo.Outputs = projectInfo.Outputs + initProjectInfo.Outputs
                ProjectInfo.Dependencies = projectInfo.Dependencies + initProjectInfo.Dependencies }) ProjectInfo.Default

    let dependsOn =
        // collect dependencies for all the project
        // NOTE we are keeping only project dependencies as we want to construct project graph
        projectConfig.Project.DependsOn |> Option.defaultValue Set.empty
        |> Set.union (Dependencies.reflectionFind projectConfig)
        |> Set.choose (fun dep -> if dep.StartsWith("project.") then Some dep else None)

    let labels = projectConfig.Project.Labels
    let types = projectConfig.Project.Initializers

    let projectTargets =
        // apply target override
        let buildProjectTargets() =
            projectConfig.Targets |> Map.map (fun targetName targetBlock ->
                // apply workspace default value
                let workspaceTarget = workspaceConfig.Targets |> Map.tryFind targetName
                let build = targetBlock.Build |> Option.orElseWith (fun () -> workspaceTarget |> Option.bind _.Build)
                let dependsOn = targetBlock.DependsOn |> Option.orElseWith (fun () -> workspaceTarget |> Option.bind _.DependsOn)
                let cache = targetBlock.Cache |> Option.orElseWith (fun () -> workspaceTarget |> Option.bind _.Cache)
                { targetBlock with 
                    Build = build
                    DependsOn = dependsOn
                    Cache = cache })
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
            Log.Debug("Enabling project '{ProjectId}'", projectDir)
            buildProjectTargets()
        else
            Log.Debug("Disabling project '{ProjectId}'", projectDir)
            Map.empty

    // convert relative dependencies to absolute dependencies respective to workspaceDirectory
    let projectDependencies =
        projectConfig.Project.Dependencies |> evalAsStringSet
        |> Set.union initProjectInfo.Dependencies
        |> Set.map (fun dep -> FS.workspaceRelative options.Workspace projectDir dep)

    let projectIncludes =
        projectScripts
        |> Seq.choose (fun (KeyValue(_, script)) -> script)
        |> Set.ofSeq
        |> Set.union (projectConfig.Project.Includes |> evalAsStringSet)

    let projectIgnores = projectConfig.Project.Ignores |> evalAsStringSet
    let projectOutputs = projectConfig.Project.Outputs |> evalAsStringSet |> Set.union initProjectInfo.Outputs

    // enrich workspace locals with project locals
    // NOTE we are checking for duplicated fields as this is an error
    let locals =
        workspaceConfig.Locals |> Map.iter (fun name _ ->
            if projectConfig.Locals |> Map.containsKey name then raiseParseError $"duplicated local '{name}'")
        workspaceConfig.Locals |> Map.addMap projectConfig.Locals

    { LoadedProject.Id = projectConfig.Project.Id
      LoadedProject.DependsOn = dependsOn
      LoadedProject.Dependencies = projectDependencies
      LoadedProject.Includes = projectIncludes
      LoadedProject.Ignores = projectIgnores
      LoadedProject.Outputs = projectOutputs
      LoadedProject.Targets = projectTargets
      LoadedProject.Labels = labels
      LoadedProject.Types = types
      LoadedProject.Extensions = extensions
      LoadedProject.Scripts = scripts
      LoadedProject.Locals = locals }



// this is the final stage: create targets and create the project
let private finalizeProject workspaceDir projectDir evaluationContext (projectDef: LoadedProject) (projectDependencies: Map<string, Project>) =
    let startFinalize = DateTime.UtcNow
    let projectId = projectDir |> String.toLower

    // get dependencies on files
    let committedFiles = Git.enumeratedCommittedFiles workspaceDir projectDir |> Set.ofList
    let additionalFiles =
        projectDir
        |> IO.enumerateFilesBut projectDef.Includes (projectDef.Outputs + projectDef.Ignores)
        |> Set
    let files = committedFiles + additionalFiles |> Set

    let sortedFiles =
        files
        |> Seq.sort
        |> List.ofSeq

    let filesHash =
        sortedFiles
        |> Hash.sha256files

    let dependenciesHash =
        let versionDependencies = projectDependencies |> Map.map (fun _ depProj -> depProj.Hash)
        versionDependencies.Values
        |> Seq.sort
        |> Hash.sha256strings

    // NOTE: this is the hash (modulo target name) used for reconcialiation across executions
    let projectHash =
        let relativeSortedFiles = 
            sortedFiles
            |> List.map (fun file -> FS.relativePath projectDir file)
        [ projectId; filesHash; dependenciesHash ] @ relativeSortedFiles
        |> Hash.sha256strings

    let evaluationContext = 
        let terrabuildProjectVars =
            Map [ if projectDef.Id.IsSome then "terrabuild.project", Value.String projectDef.Id.Value
                  "terrabuild.project_slug", projectDir |> String.slugify |> Value.String 
                  "terrabuild.version", Value.String projectHash ]
  
        let projectVars =
            projectDependencies |> Seq.choose (fun (KeyValue(_, project)) ->
                project.Id |> Option.map (fun id ->
                    $"project.{id}", Value.Map (Map ["version", Value.String project.Hash])))
            |> Map.ofSeq

        { evaluationContext with
            Eval.Data =
                evaluationContext.Data
                |> Map.addMap terrabuildProjectVars
                |> Map.addMap projectVars }

    let projectSteps =
        projectDef.Targets |> Map.map (fun targetName target ->
            let evaluationContext =
                let mutable evaluationContext =
                    let terrabuildTargetVars =
                        Map [ "terrabuild.target" , Value.String targetName ]

                    { evaluationContext with
                        Eval.ProjectDir = Some projectDir
                        Eval.Data =
                            evaluationContext.Data
                            |> Map.addMap terrabuildTargetVars }

                // build the values
                let localsHub = Hub.Create(1)

                // bootstrap
                for (KeyValue(name, value)) in evaluationContext.Data do
                    localsHub.Subscribe name [] (fun () ->
                        let varSignal = localsHub.GetSignal<Value> name
                        varSignal.Set(value))

                for (KeyValue(name, localExpr)) in projectDef.Locals do
                    let localName = $"local.{name}"
                    let deps = Dependencies.find localExpr
                    let signalDeps =
                        deps
                        |> Seq.map (fun dep -> localsHub.GetSignal<Value> dep)
                        |> List.ofSeq
                    localsHub.Subscribe localName signalDeps (fun () ->
                        let localValue = Eval.eval evaluationContext localExpr
                        evaluationContext <- { evaluationContext with Data = evaluationContext.Data |> Map.add localName localValue }
                        let localSignal = localsHub.GetSignal<Value> localName
                        localSignal.Set(localValue))

                match localsHub.WaitCompletion() with
                | Status.Ok -> evaluationContext
                | Status.UnfulfilledSubscription (subscription, signals) ->
                    let unraisedSignals = signals |> String.join ","
                    raiseInvalidArg $"Failed to evaluate '{subscription}': local value '{unraisedSignals}' is not declared."
                | Status.SubscriptionError edi ->
                    forwardExternalError("Failed to evaluate locals", edi.SourceException)

            // use value from project target
            // otherwise use workspace target
            // defaults to allow caching
            let targetBuild =
                match target.Build with
                | None -> None
                | Some targetBuild ->
                    let targetBuild = targetBuild |> Eval.eval evaluationContext |> Eval.asEnum
                    match targetBuild with
                    | Ok "auto" -> Some Build.Auto
                    | Ok "cascade" -> Some Build.Cascade
                    | Ok "always" -> Some Build.Always
                    | Ok x -> raiseParseError $"Invalid build value '{x}'"
                    | Error error -> raiseParseError error

            let targetBatch, targetOperations =
                target.Steps |> List.fold (fun (targetBatch, targetOperations) step ->
                    let extension = 
                        match projectDef.Extensions |> Map.tryFind step.Extension with
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

                    let script =
                        match Extensions.getScript step.Extension projectDef.Scripts with
                        | Some script -> script
                        | _ -> raiseSymbolError $"Extension {step.Extension} is not defined"

                    let variables =
                        extension.Variables
                        |> Option.bind (Eval.asStringSetOption << Eval.eval evaluationContext)
                        |> Option.defaultValue Set.empty

                    let batch =
                        extension.Batch
                        |> Option.bind (Eval.asBoolOption << Eval.eval evaluationContext)
                        |> Option.defaultValue false

                    let hash =
                        let containerDeps =
                            match image with
                            | Some container ->
                                let lstVariables = variables |> List.ofSeq |> List.sort
                                let lstPlatform = platform |> Option.map (fun p -> [ p ]) |> Option.defaultValue []
                                container :: lstVariables @ lstPlatform
                            | _ -> []

                        [ step.Extension; step.Command ] @ containerDeps
                        |> Hash.sha256strings

                    let targetContext = {
                        TargetOperation.Hash = hash
                        TargetOperation.Image = image
                        TargetOperation.Platform = platform
                        TargetOperation.ContainerVariables = variables
                        TargetOperation.Extension = step.Extension
                        TargetOperation.Command = step.Command
                        TargetOperation.Script = script
                        TargetOperation.Context = context
                    }

                    let operations = targetOperations @ [ targetContext ]
                    (targetBatch && batch, operations)
                ) (true, [])

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
                    | Ok "none" -> Some Artifacts.None
                    | Ok "workspace" -> Some Artifacts.Workspace
                    | Ok "managed" -> Some Artifacts.Managed
                    | Ok "external" -> Some Artifacts.External
                    | Ok x -> raiseParseError $"Invalid artifacts value '{x}'"
                    | Error error -> raiseParseError error

            let targetHash =
                targetOperations
                |> List.map (fun ope -> ope.Hash)
                |> Hash.sha256strings

            let target =
                { Target.Hash = targetHash
                  Target.Build = targetBuild
                  Target.Batch = targetBatch
                  Target.DependsOn = targetDependsOn
                  Target.Cache = targetCache
                  Target.Outputs = targetOutputs
                  Target.Operations = targetOperations }

            target
        )

    let relativeFiles = files |> Set.map (FS.relativePath projectDir)

    let projectDependencies = projectDependencies.Keys |> Seq.map String.toLower |> Set.ofSeq

    let endFinalize = DateTime.UtcNow
    Log.Debug("Finalized project '{ProjectId}' for {Duration}", projectDir, endFinalize - startFinalize)

    { Project.Id = projectDef.Id
      Project.Directory = projectDir
      Project.Hash = projectHash
      Project.Dependencies = projectDependencies
      Project.Files = relativeFiles
      Project.Targets = projectSteps
      Project.Labels = projectDef.Labels
      Project.Types = projectDef.Types }




let read (options: ConfigOptions.Options) =
    $"{Ansi.Emojis.unicorn} Settings" |> Terminal.writeLine

    let workspaceContent = FS.combinePath options.Workspace "WORKSPACE" |> File.ReadAllText
    let workspaceConfig =
        try
            FrontEnd.Workspace.parse workspaceContent
        with exn ->
            raiseParserError("Failed to read WORKSPACE configuration file", exn)

    let engine =
        match options.Engine |> Option.orElse workspaceConfig.Workspace.Engine with
        | Some "docker" | None -> "docker" |> Some
        | Some "podman" -> "podman" |> Some
        | Some "none" -> None
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
            if options.WhatIf then "whatif" ] |> String.join(" ")    
        [
            if warningConfig |> String.IsNullOrWhiteSpace |> not then $"Build flags [{warningConfig}]"
            if options.Engine.IsSome then $"Engine {options.Engine.Value}"
            if options.Run.IsSome then $"Source control {options.Run.Value.Name}"
            if options.Configuration.IsSome then $"Configuration {options.Configuration.Value}"
            if options.Environment.IsSome then $"Environment {options.Environment.Value}"
            $"Targets [{targets}]"
            if labels.IsSome then $"Labels [{labels.Value}]"
            if types.IsSome then $"Types [{types.Value}]"
            if projects.IsSome then $"Types [{projects.Value}]"
        ]
    configInfos |> List.iter (fun configInfo -> $" {Ansi.Styles.green}{Ansi.Emojis.arrow}{Ansi.Styles.reset} {configInfo}" |> Terminal.writeLine)

    // check min version requirement
    match workspaceConfig.Workspace.Version with
    | Some minVersion ->
        let actualVersion = Version.version()
        if actualVersion |> Version.isAtLeast minVersion |> not then
            raiseInvalidArg $"Workspace requires version '{minVersion}' or newer (found '{actualVersion}')."
    | _ -> ()

    $"{Ansi.Emojis.bolt} Building graph" |> Terminal.writeLine

    let evaluationContext = buildEvaluationContext engine options workspaceConfig

    let scripts = buildScripts options workspaceConfig evaluationContext

    let extensions = Extensions.systemExtensions |> Map.addMap workspaceConfig.Extensions

    let searchProjectsAndApply() =
        let workspaceIgnores = workspaceConfig.Workspace.Ignores |> Option.defaultValue default_ignores
        let scanFolder = scanFolders options.Workspace workspaceIgnores
        let projectLoading = ConcurrentDictionary<string, bool>()
        let projectIds = ConcurrentDictionary<string, string>()
        let projects = ConcurrentDictionary<string, Project>()
        let hub = Hub.Create(options.MaxConcurrency)

        let rec loadProject projectDir =
            let projectPathId = projectDir |> String.toLower
            if projectLoading.TryAdd(projectPathId, true) then

                // parallel load of projects
                hub.SubscribeBackground projectDir [] (fun () ->
                    let loadedProject =
                        try
                            // load project and force loading all dependencies as well
                            let loadedProject = loadProjectDef options workspaceConfig evaluationContext extensions scripts projectDir
                            match loadedProject.Id with
                            | Some projectId ->
                                if projectIds.TryAdd(projectId, projectDir) |> not then
                                    raiseSymbolError $"Project id '{projectId}' is already defined in project '{projectIds[projectId]}'"
                            | _ -> ()

                            loadedProject
                        with exn ->
                            raiseParserError($"Failed to read PROJECT configuration '{projectDir}'", exn)

                    // immediately load all dependencies
                    for dependency in loadedProject.Dependencies do
                        loadProject dependency

                    // await dependencies to be loaded
                    let projectPathSignals =
                        loadedProject.Dependencies
                        |> Set.map String.toLower
                        |> Seq.map (fun awaitedProjectId -> hub.GetSignal<Project> awaitedProjectId)
                        |> List.ofSeq

                    let dependsOnSignals =
                        loadedProject.DependsOn
                        |> Seq.map (fun awaitedProjectId -> hub.GetSignal<Project> awaitedProjectId)
                        |> List.ofSeq

                    let awaitedSignals = projectPathSignals @ dependsOnSignals
                    hub.SubscribeBackground projectDir awaitedSignals (fun () ->
                        try
                            // build task & code & notify
                            let dependsOnProjects = 
                                awaitedSignals
                                |> Seq.map (fun projectDependency -> projectDependency.Get<Project>().Directory, projectDependency.Get<Project>())
                                |> Map.ofSeq

                            let project = finalizeProject options.Workspace projectDir evaluationContext loadedProject dependsOnProjects
                            if projects.TryAdd(projectPathId, project) |> not then raiseBugError "Unexpected error"

                            let loadedProjectPathIdSignal = hub.GetSignal<Project> projectPathId
                            loadedProjectPathIdSignal.Set(project)

                            match loadedProject.Id with
                            | Some projectId ->
                                let loadedProjectIdSignal = hub.GetSignal<Project> $"project.{projectId}"
                                loadedProjectIdSignal.Set(project)
                            | _ -> ()
                        with exn -> forwardExternalError($"Error while parsing project '{projectDir}'", exn)))

        let rec findDependencies isRoot dir =
            if isRoot || scanFolder  dir then
                let projectFile = FS.combinePath dir "PROJECT" 
                match projectFile with
                | FS.File file ->
                    let projectFile = file |> FS.parentDirectory |> Option.get |> FS.relativePath options.Workspace
                    try
                        loadProject projectFile
                    with exn -> forwardExternalError($"Error while parsing project '{projectFile}'", exn)
                | _ ->
                    for subdir in dir |> IO.enumerateDirs do
                        findDependencies false subdir

        findDependencies true options.Workspace
        let status = hub.WaitCompletion()

        match status with
        | Status.Ok ->
            Log.Debug("Configuration successful")
            projects |> Map.ofDict
        | Status.UnfulfilledSubscription (subscription, signals) ->
            let unraisedSignals = signals |> String.join ","
            Log.Fatal($"Configuration '{subscription}' has pending operations on '{unraisedSignals}'")
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

    // select dependencies with id if any
    let projectSelection =
        match options.Projects with
        | Some filter -> projectSelection |> Map.filter (fun _ config ->
            config.Id
            |> Option.map(fun id -> filter |> Set.contains id)
            |> Option.defaultValue false)
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
          Workspace.Targets = targets }
    options, workspaceConfig
