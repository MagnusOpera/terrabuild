module GraphPipeline.Node
open Collections
open System.Collections.Generic
open Errors
open Serilog
open System
open GraphDef



// build the high-level graph from configuration
let build (options: ConfigOptions.Options) (configuration: Configuration.Workspace) =
    let startedAt = DateTime.UtcNow
    let allNodes = Dictionary<string, Node>()
    let processedNodes = Dictionary<string, bool>()

    // first check all targets exist in WORKSPACE
    match options.Targets |> Seq.tryFind (fun targetName -> configuration.Targets |> Map.containsKey targetName |> not) with
    | Some undefinedTarget -> raiseSymbolError $"Target {undefinedTarget} is not defined in WORKSPACE"
    | _ -> ()


    let buildDependsOn (projectConfig: Configuration.Project) target =
        let buildDependsOn =
            configuration.Targets
            |> Map.tryFind target
            |> Option.defaultValue Set.empty
        let projDependsOn =
            projectConfig.Targets
            |> Map.tryFind target
            |> Option.map (fun ct -> ct.DependsOn)
            |> Option.defaultValue Set.empty
        let dependsOns = buildDependsOn + projDependsOn
        dependsOns


    let buildDependencies (projectConfig: Configuration.Project) (project: string) (target: string) =
        let dependsOns = buildDependsOn projectConfig target

        dependsOns
        |> Seq.collect (fun dependsOn ->
            match dependsOn with
            // Cross-project dependency: ^<target>
            | String.Regex "^\^(.+)$" [ depTarget ] ->
                projectConfig.Dependencies
                |> Seq.choose (fun depProject ->
                    let depConfig = configuration.Projects[depProject]
                    if depConfig.Targets |> Map.containsKey depTarget then
                        Some (depProject, depTarget)
                    else
                        None)

            // Intra-project dependency: <target>
            | depTarget ->
                if projectConfig.Targets |> Map.containsKey depTarget then
                    Seq.singleton (project, depTarget)
                else
                    Seq.empty
        )
        |> Set.ofSeq

    let rec buildNode project target =
        let projectConfig = configuration.Projects[project]
        let targetConfig = projectConfig.Targets[target]
        let nodeId = $"{project}:{target}"

        let processNode() =
            let allDeps = buildDependencies projectConfig project target
            let children = allDeps |> Set.map (fun (project, target) -> $"{project}:{target}")

            // ensure children exist
            for (project, target) in allDeps do
                buildNode project target

            let cachable, batchable, ops =
                targetConfig.Operations |> List.fold (fun (_, batchable, ops) operation ->
                    let optContext =
                        { Terrabuild.ScriptingContracts.ActionContext.Debug = options.Debug
                          Terrabuild.ScriptingContracts.ActionContext.CI = options.Run.IsSome
                          Terrabuild.ScriptingContracts.ActionContext.Command = operation.Command
                          Terrabuild.ScriptingContracts.ActionContext.Hash = projectConfig.Hash
                          Terrabuild.ScriptingContracts.ActionContext.Directory = projectConfig.Directory
                          Terrabuild.ScriptingContracts.ActionContext.Batch = None }

                    let parameters = 
                        match operation.Context with
                        | Terrabuild.Expressions.Value.Map map ->
                            map
                            |> Map.add "context" (Terrabuild.Expressions.Value.Object optContext)
                            |> Terrabuild.Expressions.Value.Map
                        | _ -> raiseBugError "Failed to get context (internal error)"

                    let cacheability =
                        match Extensions.getScriptCacheability optContext.Command (Some operation.Script) with
                        | Some cacheability ->
                            match cacheability with
                            | Terrabuild.ScriptingContracts.Cacheability.Never -> ArtifactMode.None
                            | Terrabuild.ScriptingContracts.Cacheability.Local -> ArtifactMode.Workspace
                            | Terrabuild.ScriptingContracts.Cacheability.Remote -> ArtifactMode.Managed
                            | Terrabuild.ScriptingContracts.Cacheability.External -> ArtifactMode.External
                        | _ -> raiseInvalidArg $"Failed to get cacheability for command {operation.Extension} {optContext.Command}"

                    let shellOperations =
                        match Extensions.invokeScriptMethod<Terrabuild.ScriptingContracts.ShellOperations> optContext.Command parameters (Some operation.Script) with
                        | Extensions.InvocationResult.Success executionRequest -> executionRequest
                        | Extensions.InvocationResult.ErrorTarget ex -> forwardExternalError($"{hash}: Failed to get shell operation (extension error)", ex)
                        | _ -> raiseInvalidArg $"{hash}: Failed to get shell operation (extension error)"

                    let newops =
                        shellOperations |> List.map (fun shellOperation -> {
                            ContaineredShellOperation.Image = operation.Image
                            ContaineredShellOperation.Platform = operation.Platform
                            ContaineredShellOperation.Cpus = operation.Cpus
                            ContaineredShellOperation.Variables = operation.ContainerVariables
                            ContaineredShellOperation.Envs = operation.Envs
                            ContaineredShellOperation.MetaCommand = $"{operation.Extension} {operation.Command}"
                            ContaineredShellOperation.Command = shellOperation.Command
                            ContaineredShellOperation.Arguments = shellOperation.Arguments |> String.normalizeShellArgs
                            ContaineredShellOperation.ErrorLevel = shellOperation.ErrorLevel })

                    let batchable = 
                        match Extensions.isScriptBatchable optContext.Command (Some operation.Script) with
                        | true -> batchable
                        | _ -> false

                    cacheability, batchable, ops @ newops
                ) (ArtifactMode.Managed, true, [])

            let opsCmds = ops |> List.map Json.Serialize

            let targetContent = opsCmds @ [
                yield projectConfig.Hash
                yield targetConfig.Hash
                yield! children |> Seq.map (fun nodeId -> allNodes[nodeId].TargetHash)
            ]
            let targetHash = targetContent |> Hash.sha256strings

            // cacheability can be overriden by the target
            let cache = targetConfig.Cache |> Option.defaultValue cachable

            // auto build by default unless force
            let build =
                if options.Force then BuildMode.Always
                else targetConfig.Build |> Option.defaultValue BuildMode.Auto

            let required = build = BuildMode.Always

            let targetOutput =
                if cache = ArtifactMode.None then Set.empty
                else targetConfig.Outputs

            let targetClusterHash =
                if batchable then Some targetConfig.Hash
                else None

            let node =
                { Node.Id = nodeId

                  Node.ProjectId = projectConfig.Id
                  Node.ProjectName = projectConfig.Name
                  Node.ProjectDir = projectConfig.Directory
                  Node.Target = target
                  Node.Operations = ops
                  Node.Artifacts = cache
                  Node.Build = build
                  Node.Batch = targetConfig.Batch
                  Node.Dependencies = children
                  Node.Outputs = targetOutput

                  Node.ClusterHash = targetClusterHash
                  Node.ProjectHash = projectConfig.Hash
                  Node.TargetHash = targetHash
                  Node.Action = RunAction.Ignore
                  Node.Required = required }

            Log.Debug("Node '{NodeId}' has key '{Key}'", nodeId, buildCacheKey node)
            if allNodes.TryAdd(nodeId, node) |> not then raiseBugError "Unexpected graph building race"
  
        if processedNodes.TryAdd(nodeId, true) then processNode()

    configuration.SelectedProjects |> Seq.iter (fun project ->
        options.Targets |> Seq.iter (fun target ->
            configuration.Projects
            |> Map.tryFind project
            |> Option.iter (fun projectConfig -> if projectConfig.Targets |> Map.containsKey target then buildNode project target)))

    let rootNodes =
        let allNodeIds = allNodes.Keys |> Set
        let allDependencyIds = allNodes.Values |> Seq.collect (fun node -> node.Dependencies) |> Set.ofSeq
        allNodeIds - allDependencyIds

    let endedAt = DateTime.UtcNow
    let buildDuration = endedAt - startedAt
    Log.Debug("Graph Build: {duration}", buildDuration)

    $" {Ansi.Styles.green}{Ansi.Emojis.arrow}{Ansi.Styles.reset} {allNodes.Count} nodes" |> Terminal.writeLine
    $" {Ansi.Styles.green}{Ansi.Emojis.arrow}{Ansi.Styles.reset} {rootNodes.Count} root nodes" |> Terminal.writeLine

    { Graph.Nodes = allNodes |> Map.ofDict
      Graph.RootNodes = rootNodes
      Graph.Batches = Map.empty }
