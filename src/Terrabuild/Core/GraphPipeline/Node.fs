module GraphPipeline.Node
open Collections
open System.Collections.Concurrent
open Errors
open Serilog
open System
open GraphDef



// build the high-level graph from configuration
let build (options: ConfigOptions.Options) (configuration: Configuration.Workspace) =
    let startedAt = DateTime.UtcNow
    Log.Debug("===== [Graph Build] =====")

    let allNodes = ConcurrentDictionary<string, Node>()
    let processedNodes = ConcurrentDictionary<string, bool>()

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


    let buildOuterTargets (projectConfig: Configuration.Project) target = [
        let dependsOns = buildDependsOn projectConfig target
        for dependsOn in dependsOns do
            match dependsOn with
            | String.Regex "^\^(.+)$" [ depTarget ] ->
                for depProject in projectConfig.Dependencies do
                    let depConfig = configuration.Projects[depProject]
                    if depConfig.Targets |> Map.containsKey depTarget then (depProject, depTarget)
            | _ -> ()
    ]


    let rec buildNode project target =
        let projectConfig = configuration.Projects[project]
        let targetConfig = projectConfig.Targets[target]
        let nodeId = $"{project}:{target}"


        let rec buildInnerTargets target = [
            let dependsOns = buildDependsOn projectConfig target
            yield! dependsOns |> Seq.collect (fun dependsOn ->
                match dependsOn with
                | String.Regex "^\^(.+)$" _ -> []
                | target ->
                    if projectConfig.Targets |> Map.containsKey target then [ (project, target) ]
                    else buildInnerTargets target)
        ]



        let processNode() =
            let outerDeps = buildOuterTargets projectConfig target |> Set.ofSeq
            let innerDeps = buildInnerTargets target |> Set.ofSeq

            let allDeps = innerDeps + outerDeps
            let children = allDeps |> Set.map (fun (project, target) -> $"{project}:{target}")

            // ensure children exist
            for (project, target) in allDeps do
                buildNode project target

            let cachable, batchable, ops =
                targetConfig.Operations |> List.fold (fun (_, batchable, ops) operation ->
                    let optContext =
                        { Terrabuild.Extensibility.ActionContext.Debug = options.Debug
                          Terrabuild.Extensibility.ActionContext.CI = options.Run.IsSome
                          Terrabuild.Extensibility.ActionContext.Command = operation.Command
                          Terrabuild.Extensibility.ActionContext.Hash = projectConfig.Hash
                          Terrabuild.Extensibility.ActionContext.Batch = None }

                    let parameters = 
                        match operation.Context with
                        | Terrabuild.Expressions.Value.Map map ->
                            map
                            |> Map.add "context" (Terrabuild.Expressions.Value.Object optContext)
                            |> Terrabuild.Expressions.Value.Map
                        | _ -> raiseBugError "Failed to get context (internal error)"

                    let cacheability =
                        match Extensions.getScriptAttribute<Terrabuild.Extensibility.CacheableAttribute> optContext.Command (Some operation.Script) with
                        | Some attr ->
                            match attr.Cacheability with
                            | Terrabuild.Extensibility.Cacheability.Never -> Cacheability.Never
                            | Terrabuild.Extensibility.Cacheability.Local -> Cacheability.Local
                            | Terrabuild.Extensibility.Cacheability.Remote -> Cacheability.Remote
                            | Terrabuild.Extensibility.Cacheability.External -> Cacheability.External
                        | _ -> raiseBugError $"Failed to get cacheability for command {operation.Extension} {optContext.Command}"

                    let shellOperations =
                        match Extensions.invokeScriptMethod<Terrabuild.Extensibility.ShellOperations> optContext.Command parameters (Some operation.Script) with
                        | Extensions.InvocationResult.Success executionRequest -> executionRequest
                        | Extensions.InvocationResult.ErrorTarget ex -> forwardExternalError($"{hash}: Failed to get shell operation (extension error)", ex)
                        | _ -> raiseExternalError $"{hash}: Failed to get shell operation (extension error)"

                    let newops =
                        shellOperations |> List.map (fun shellOperation -> {
                            ContaineredShellOperation.Container = operation.Container
                            ContaineredShellOperation.ContainerPlatform = operation.Platform
                            ContaineredShellOperation.ContainerVariables = operation.ContainerVariables
                            ContaineredShellOperation.MetaCommand = $"{operation.Extension} {operation.Command}"
                            ContaineredShellOperation.Command = shellOperation.Command
                            ContaineredShellOperation.Arguments = shellOperation.Arguments |> String.normalizeShellArgs })

                    let batchable = 
                        match Extensions.getScriptAttribute<Terrabuild.Extensibility.BatchableAttribute> optContext.Command (Some operation.Script) with
                        | Some _ -> batchable
                        | _ -> false

                    cacheability, batchable, ops @ newops
                ) (Cacheability.Remote, targetConfig.Batch, [])

            let opsCmds = ops |> List.map Json.Serialize

            let targetContent = opsCmds @ [
                yield projectConfig.Hash
                yield targetConfig.Hash
                yield! children |> Seq.map (fun nodeId -> allNodes[nodeId].TargetHash)
            ]
            let targetHash = targetContent |> Hash.sha256strings

            Log.Debug($"Node {nodeId} has ProjectHash {projectConfig.Hash} and TargetHash {targetHash}")

            // cacheability can be overriden by the target
            let cache = targetConfig.Cache |> Option.defaultValue cachable

            // no rebuild by default unless force
            let rebuild =
                let defaultForce = if options.Force then Rebuild.Always else Rebuild.Auto
                targetConfig.Rebuild |> Option.defaultValue defaultForce
            let buildAction =
                if rebuild = Rebuild.Always then NodeAction.Build
                else NodeAction.Ignore

            let targetOutput = targetConfig.Outputs

            let batchContent = [
                targetConfig.Hash
                $"{buildAction}"
            ]
            let batchHash = batchContent |> Hash.sha256strings

            let targetClusterHash =
                if targetConfig.Batch && batchable then batchHash
                else targetHash // this is expected to be unique to disable node clustering

            let node =
                { Node.Id = nodeId

                  Node.ProjectId = projectConfig.Id
                  Node.ProjectDir = projectConfig.Directory
                  Node.Target = target
                  Node.Operations = ops
                  Node.Cache = cache
                  Node.Rebuild = rebuild

                  Node.Dependencies = children
                  Node.Outputs = targetOutput

                  Node.ClusterHash = targetClusterHash
                  Node.ProjectHash = projectConfig.Hash
                  Node.TargetHash = targetHash

                  Node.Action = buildAction }
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
      Graph.Clusters = Map.empty }
