module GraphPipeline.Resolve

open Collections
open System.Collections.Generic
open Errors
open Serilog
open System
open GraphDef

let private resolveCacheability extensionName command script =
    match Extensions.getScriptCacheability command (Some script) with
    | Some cacheability ->
        match cacheability with
        | Terrabuild.ScriptingContracts.Cacheability.Never -> ArtifactMode.None
        | Terrabuild.ScriptingContracts.Cacheability.Local -> ArtifactMode.Workspace
        | Terrabuild.ScriptingContracts.Cacheability.Remote -> ArtifactMode.Managed
        | Terrabuild.ScriptingContracts.Cacheability.External -> ArtifactMode.External
    | _ -> raiseInvalidArg $"Failed to get cacheability for command {extensionName} {command}"

let internal resolveTargetOperations
    (options: ConfigOptions.Options)
    (projectConfig: Configuration.Project)
    (targetConfig: Configuration.Target)
    (contextHash: string)
    (batchContext: Terrabuild.ScriptingContracts.BatchContext option) =
    targetConfig.Steps
    |> List.fold (fun (_, batchable, ops) step ->
        let optContext =
            { Terrabuild.ScriptingContracts.ActionContext.Debug = options.Debug
              Terrabuild.ScriptingContracts.ActionContext.CI = options.Run.IsSome
              Terrabuild.ScriptingContracts.ActionContext.Command = step.Command
              Terrabuild.ScriptingContracts.ActionContext.Hash = contextHash
              Terrabuild.ScriptingContracts.ActionContext.Directory = projectConfig.Directory
              Terrabuild.ScriptingContracts.ActionContext.Batch = batchContext }

        let parameters =
            match step.Context with
            | Terrabuild.Expression.Value.Map map ->
                map
                |> Map.add "context" (Terrabuild.Expression.Value.Object optContext)
                |> Terrabuild.Expression.Value.Map
            | _ -> raiseBugError "Failed to get context (internal error)"

        let cacheability = resolveCacheability step.Extension step.Command step.Script

        let executionResult =
            match Extensions.invokeScriptMethod<Terrabuild.ScriptingContracts.CommandResult> optContext.Command parameters (Some step.Script) with
            | Extensions.InvocationResult.Success executionRequest -> executionRequest
            | Extensions.InvocationResult.ErrorTarget ex -> forwardExternalError($"{contextHash}: Failed to get shell operation (extension error)", ex)
            | _ -> raiseInvalidArg $"{contextHash}: Failed to get shell operation (extension error)"

        let newops =
            executionResult.Operations |> List.map (fun shellOperation -> {
                ContaineredShellOperation.Image = step.Image
                ContaineredShellOperation.Platform = step.Platform
                ContaineredShellOperation.Cpus = step.Cpus
                ContaineredShellOperation.Variables = step.ContainerVariables
                ContaineredShellOperation.Envs = step.Envs
                ContaineredShellOperation.MetaCommand = $"{step.Extension} {step.Command}"
                ContaineredShellOperation.Command = shellOperation.Command
                ContaineredShellOperation.Arguments = shellOperation.Arguments |> String.normalizeShellArgs
                ContaineredShellOperation.ErrorLevel = shellOperation.ErrorLevel })

        let batchable = batchable && executionResult.Batchable

        cacheability, batchable, ops @ newops
    ) (ArtifactMode.Managed, true, [])

let build (options: ConfigOptions.Options) (configuration: Configuration.Workspace) (graph: Graph) =
    let startedAt = DateTime.UtcNow
    let allNodes = Dictionary<string, Node>()
    let processedNodes = Dictionary<string, bool>()

    let rec resolveNode nodeId =
        let sourceNode = graph.Nodes[nodeId]
        let projectConfig = configuration.Projects[sourceNode.ProjectId]
        let targetConfig = projectConfig.Targets[sourceNode.Target]

        let processNode () =
            for childId in sourceNode.Dependencies do
                resolveNode childId

            let cachable, batchable, ops =
                resolveTargetOperations options projectConfig targetConfig projectConfig.Hash None

            let opsCmds = ops |> List.map Json.Serialize
            let targetContent =
                opsCmds @ [
                    yield projectConfig.Hash
                    yield targetConfig.Hash
                    yield! sourceNode.Dependencies |> Seq.map (fun childId -> allNodes[childId].TargetHash)
                ]
            let targetHash = targetContent |> Hash.sha256strings

            let cache = targetConfig.Cache |> Option.defaultValue cachable
            let targetOutput =
                if cache = ArtifactMode.None then Set.empty
                else targetConfig.Outputs
            let targetClusterHash =
                if batchable then Some targetConfig.Hash
                else None

            let node =
                { sourceNode with
                    Operations = ops
                    Artifacts = cache
                    Outputs = targetOutput
                    ClusterHash = targetClusterHash
                    TargetHash = targetHash }

            Log.Debug("Resolved node '{NodeId}' with key '{Key}'", nodeId, buildCacheKey node)
            if allNodes.TryAdd(nodeId, node) |> not then raiseBugError "Unexpected graph resolving race"

        if processedNodes.TryAdd(nodeId, true) then processNode ()

    graph.Nodes
    |> Map.keys
    |> Seq.iter resolveNode

    let endedAt = DateTime.UtcNow
    let buildDuration = endedAt - startedAt
    Log.Debug("Graph Resolve: {duration}", buildDuration)

    { graph with
        Graph.Nodes = allNodes |> Map.ofDict }
