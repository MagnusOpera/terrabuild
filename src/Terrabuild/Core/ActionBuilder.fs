
module ActionBuilder


open System
open Collections
open Serilog
open Terrabuild.PubSub


let build (options: ConfigOptions.Options) (cache: Cache.ICache) (graph: GraphDef.Graph) =
    let allowRemoteCache = options.LocalOnly |> not
    let nodeResults = Concurrent.ConcurrentDictionary<string, GraphDef.NodeAction>()
    let scheduledNodeStatus = Concurrent.ConcurrentDictionary<string, bool>()
    let hub = Hub.Create(options.MaxConcurrency)

    let computeNodeAction (node: GraphDef.Node) maxCompletionChildren =
        if node.Action = GraphDef.NodeAction.Build then
            Log.Debug("{NodeId} must rebuild because force requested", node.Id)
            (GraphDef.NodeAction.Build, DateTime.MinValue)

        elif node.Cache <> Terrabuild.Extensibility.Cacheability.Never then
            let cacheEntryId = GraphDef.buildCacheKey node
            match cache.TryGetSummaryOnly allowRemoteCache cacheEntryId with
            | Some (_, summary) ->
                Log.Debug("{NodeId} has existing build summary", node.Id)

                // retry requested and task is failed
                if options.Retry && (not summary.IsSuccessful) then
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


    let rec scheduleNodeStatus lineage nodeId =
        if scheduledNodeStatus.TryAdd(nodeId, true) then
            let node = graph.Nodes[nodeId]

            // get the status of dependencies
            let dependencyStatus =
                node.Dependencies
                |> Seq.map (fun projectId ->
                    scheduleNodeStatus (Some node.ClusterId) projectId
                    hub.GetSignal<DateTime> projectId)
                |> List.ofSeq
            hub.Subscribe $"{nodeId} status" dependencyStatus (fun () ->
                // now decide what to do
                let maxCompletionChildren =
                    match dependencyStatus with
                    | [ ] -> DateTime.MinValue
                    | _ -> dependencyStatus |> Seq.map (fun dep -> dep.Get<DateTime>()) |> Seq.max
                let (buildRequest, buildDate) = computeNodeAction node maxCompletionChildren

                // only keep action with a side effect
                match lineage, buildRequest with
                | _, GraphDef.NodeAction.Ignore
                | None, GraphDef.NodeAction.Restore -> ()
                | _ -> nodeResults[nodeId] <- buildRequest

                let nodeStatusSignal = hub.GetSignal<DateTime> nodeId
                nodeStatusSignal.Set buildDate)

    graph.RootNodes |> Seq.iter (scheduleNodeStatus None)


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
        nodeResults |> Seq.fold (fun (acc: Map<string, GraphDef.Node>) (KeyValue(nodeId, nodeAction)) ->
            let node = { acc[nodeId] with GraphDef.Node.Action = nodeAction }
            acc |> Map.add nodeId node) graph.Nodes
    let rootNodes =
        graph.RootNodes
        |> Set.filter (fun nodeId -> nodes[nodeId].Action = GraphDef.NodeAction.Build)
    let graph =
        { graph with
            GraphDef.Graph.Nodes = nodes
            GraphDef.Graph.RootNodes = rootNodes }
    graph
