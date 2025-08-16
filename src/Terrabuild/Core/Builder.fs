module GraphBuilder
open Collections
open System.Collections.Concurrent
open Errors
open Serilog
open System
open GraphDef
open Terrabuild.Extensibility



// build the high-level graph from configuration
let build (options: ConfigOptions.Options) (configuration: Configuration.Workspace) =
    let startedAt = DateTime.UtcNow
    Log.Debug("===== [Graph Build] =====")

    $"{Ansi.Emojis.eyes} Building graph" |> Terminal.writeLine

    let processedNodes = ConcurrentDictionary<string, bool>()
    let allNodes = ConcurrentDictionary<string, Node>()
    let node2children = ConcurrentDictionary<string, Set<string>>()

    // first check all targets exist in WORKSPACE
    match options.Targets |> Seq.tryFind (fun targetName -> configuration.Targets |> Map.containsKey targetName |> not) with
    | Some undefinedTarget -> raiseSymbolError $"Target {undefinedTarget} is not defined in WORKSPACE"
    | _ -> ()


    let rec buildTarget targetName project =
        let projectConfig = configuration.Projects[project]
        let nodeId = $"{project}:{targetName}"

        let processNode () =
            // merge targets requirements
            let buildDependsOn =
                configuration.Targets
                |> Map.tryFind targetName
                |> Option.defaultValue Set.empty
            let projDependsOn =
                projectConfig.Targets
                |> Map.tryFind targetName
                |> Option.map (fun ct -> ct.DependsOn)
                |> Option.defaultValue Set.empty
            let dependsOns = buildDependsOn + projDependsOn

            // apply on each dependency
            let inChildren, outChildren =
                dependsOns |> Set.fold (fun (accInChildren, accOutChildren) dependsOn ->
                    match dependsOn with
                    | String.Regex "^\^(.+)$" [ parentDependsOn ] ->
                        accInChildren, accOutChildren + Set.collect (buildTarget parentDependsOn) projectConfig.Dependencies
                    | String.Regex "^(.+)$" [ dependsOn ] ->
                        accInChildren + buildTarget dependsOn project, accOutChildren
                    | _ -> raiseBugError "Invalid target dependency format") (Set.empty, Set.empty)

            // NOTE: a node is considered a leaf (within this project only) if the target has no internal dependencies detected
            let isLeaf = inChildren |> Set.isEmpty

            // only generate computation node - that is node that generate something
            // barrier nodes are just discarded and dependencies lift level up
            match projectConfig.Targets |> Map.tryFind targetName with
            | Some target ->
                let cache, ops =
                    target.Operations |> List.fold (fun (cache, ops) operation ->
                        let optContext = {
                            Terrabuild.Extensibility.ActionContext.Debug = options.Debug
                            Terrabuild.Extensibility.ActionContext.CI = options.Run.IsSome
                            Terrabuild.Extensibility.ActionContext.Command = operation.Command
                            Terrabuild.Extensibility.ActionContext.Hash = projectConfig.Hash
                        }

                        let parameters = 
                            match operation.Context with
                            | Terrabuild.Expressions.Value.Map map ->
                                map
                                |> Map.add "context" (Terrabuild.Expressions.Value.Object optContext)
                                |> Terrabuild.Expressions.Value.Map
                            | _ -> raiseBugError "Failed to get context (internal error)"

                        Log.Debug($"{hash}: Invoking extension '{operation.Extension}::{operation.Command}' with args {parameters}")

                        let cacheability =
                            match Extensions.getScriptAttribute<CacheableAttribute> optContext.Command (Some operation.Script) with
                            | Some attr -> attr.Cacheability
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

                        let cache =
                            match cacheability, options.LocalOnly with
                            | Cacheability.Never, _ -> Cacheability.Never
                            | Cacheability.Local, _ -> Cacheability.Local
                            | Cacheability.Remote, true -> Cacheability.Local
                            | Cacheability.Remote, false -> Cacheability.Remote

                        cache, ops @ newops
                    ) (Cacheability.Never, [])

                let opsCmds = ops |> List.map Json.Serialize

                let children = inChildren + outChildren
                let hashContent = opsCmds @ [
                    yield projectConfig.Hash
                    yield target.Hash
                    yield! children |> Seq.map (fun nodeId -> allNodes[nodeId].TargetHash)
                ]

                let hash = hashContent |> Hash.sha256strings

                Log.Debug($"Node {nodeId} has ProjectHash {projectConfig.Hash} and TargetHash {hash}")

                // cacheability can be overriden by the target
                let cache = target.Cache |> Option.defaultValue cache

                // if the target is explicitely requested then do not defer the node
                let deferred = target.Deferred |> Option.defaultValue (options.Targets |> Set.contains targetName |> not)

                // no rebuild by default unless force
                let rebuild = target.Rebuild |> Option.defaultValue options.Force

                let idempotent = target.Idempotent |> Option.defaultValue false

                let targetOutput = target.Outputs

                let node =
                    { Node.Id = nodeId

                      Node.ProjectId = projectConfig.Id
                      Node.ProjectDir = projectConfig.Directory
                      Node.Target = targetName
                      Node.ConfigurationTarget = target
                      Node.Operations = ops
                      Node.Cache = cache
                      Node.Rebuild = rebuild
                      Node.Deferred = deferred
                      Node.Idempotent = idempotent

                      Node.Dependencies = children
                      Node.Outputs = targetOutput

                      Node.ProjectHash = projectConfig.Hash
                      Node.TargetHash = hash

                      Node.IsLeaf = isLeaf }

                if allNodes.TryAdd(nodeId, node) |> not then raiseBugError "Unexpected graph building race"
                Set.singleton nodeId
            | _ ->
                inChildren + outChildren

        if processedNodes.TryAdd(nodeId, true) then
            let children = processNode()
            if node2children.TryAdd(nodeId, children) |> not then raiseBugError "Unexpected graph building race"
            Log.Debug($"Node {nodeId} has children: {children}")
            children
        else
            node2children[nodeId]

    let rootNodes =
        configuration.SelectedProjects |> Seq.collect (fun project ->
            options.Targets |> Seq.choose (fun target ->
                buildTarget target project |> ignore

                // identify root target
                configuration.Projects[project].Targets |> Map.tryFind target
                |> Option.map (fun _ -> $"{project}:{target}")))
        |> Set

    let endedAt = DateTime.UtcNow
    let buildDuration = endedAt - startedAt
    Log.Debug("Graph Build: {duration}", buildDuration)

    $" {Ansi.Styles.green}{Ansi.Emojis.arrow}{Ansi.Styles.reset} {allNodes.Count} nodes" |> Terminal.writeLine
    $" {Ansi.Styles.green}{Ansi.Emojis.arrow}{Ansi.Styles.reset} {rootNodes.Count} root nodes" |> Terminal.writeLine

    { Graph.Nodes = allNodes |> Map.ofDict
      Graph.RootNodes = rootNodes }
