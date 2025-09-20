
module NodeStateEvaluator


open System
open Collections
open Serilog
open Terrabuild.PubSub




let evaluate (options: ConfigOptions.Options) (cache: Cache.ICache) (graph: GraphDef.Graph) =
    let allowRemoteCache = options.LocalOnly |> not
    let retry = options.Retry

    let nodeResults = Concurrent.ConcurrentDictionary<string, GraphDef.NodeAction * string>()
    let scheduledNodeStatus = Concurrent.ConcurrentDictionary<string, bool>()
    let hub = Hub.Create(options.MaxConcurrency)


    let computeNodeAction (node: GraphDef.Node) maxCompletionChildren =
        if node.Rebuild then
            Log.Debug("{NodeId} must rebuild because force requested", node.Id)
            (GraphDef.NodeAction.Build, DateTime.MinValue)

        elif node.Cache <> Terrabuild.Extensibility.Cacheability.Never then
            let cacheEntryId = GraphDef.buildCacheKey node
            match cache.TryGetSummaryOnly allowRemoteCache cacheEntryId with
            | Some (_, summary) ->
                Log.Debug("{NodeId} has existing build summary", node.Id)

                // retry requested and task is failed
                if retry && (not summary.IsSuccessful) then
                    Log.Debug("{NodeId} must rebuild because retry requested and node is failed", node.Id)
                    (GraphDef.NodeAction.Build, DateTime.MinValue)

                // task is older than children
                elif summary.EndedAt <= maxCompletionChildren then
                    Log.Debug("{NodeId} must rebuild because child is rebuilding", node.Id)
                    (GraphDef.NodeAction.Build, DateTime.MinValue)

                // task is cached
                else
                    Log.Debug("{NodeId} is restorable {Date}", node.Id, summary.EndedAt)
                    (GraphDef.NodeAction.Restore, summary.EndedAt)
            | _ ->
                Log.Debug("{NodeId} must be built since no summary and required", node.Id)
                (GraphDef.NodeAction.Build, DateTime.MinValue)
        else
            Log.Debug("{NodeId} is not cacheable", node.Id)
            (GraphDef.NodeAction.Build, DateTime.MinValue)


    let rec scheduleNodeStatus parentGeneration parentTargetHash nodeId =
        if scheduledNodeStatus.TryAdd(nodeId, true) then
            let node = graph.Nodes[nodeId]

            // determine node generation
            // note we want deterministic generation computation
            let nodeGeneration =
                match parentTargetHash with
                | Some parentTargetHash ->
                    if node.TargetHash = parentTargetHash then parentGeneration
                    else (parentTargetHash + node.TargetHash) |> Hash.sha256
                | _ -> parentGeneration

            // get the status of dependencies
            let dependencyStatus =
                node.Dependencies
                |> Seq.map (fun projectId ->
                    scheduleNodeStatus nodeGeneration (Some node.TargetHash) projectId
                    hub.GetSignal<DateTime> $"{projectId}+status")
                |> List.ofSeq
            hub.Subscribe $"{nodeId} status" dependencyStatus (fun () ->
                let nodeStatusSignal = hub.GetSignal<DateTime> $"{nodeId}+status"

                // now decide what to do
                let maxCompletionChildren =
                    match dependencyStatus with
                    | [ ] -> DateTime.MinValue
                    | _ ->
                        dependencyStatus
                        |> Seq.maxBy (fun dep -> dep.Get<DateTime>())
                        |> (fun dep -> dep.Get<DateTime>())
                let (buildRequest, buildDate) = computeNodeAction node maxCompletionChildren
                nodeResults[nodeId] <- (buildRequest, nodeGeneration)
                nodeStatusSignal.Set buildDate)

    graph.RootNodes |> Seq.iter (scheduleNodeStatus "" None)


    let status = hub.WaitCompletion()
    match status with
    | Status.Ok ->
        Log.Debug("NodeStateEvaluator successful")
    | Status.UnfulfilledSubscription (subscription, signals) ->
        let unraisedSignals = signals |> String.join ","
        Log.Fatal($"Task '{subscription}' has pending operations on '{unraisedSignals}'")
    | Status.SubscriptionError exn ->
        Log.Fatal(exn, "BuiNodeStateEvaluatorld failed with exception")

    let nodes =
        nodeResults |> Seq.fold (fun (acc: Map<string, GraphDef.Node>) (KeyValue(nodeId, nodeResult)) ->
            let (nodeAction, nodeGeneration) = nodeResult
            let node = { acc[nodeId]
                         with
                            GraphDef.Node.Action = nodeAction
                            GraphDef.Node.Generation = nodeGeneration }
            acc |> Map.add nodeId node) graph.Nodes
    let graph = { graph with Nodes = nodes }
    graph
