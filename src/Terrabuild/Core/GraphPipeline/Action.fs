
module GraphPipeline.Action


open System
open Collections
open Serilog
open Terrabuild.PubSub
open Errors


let build (options: ConfigOptions.Options) (cache: Cache.ICache) (graph: GraphDef.Graph) =
    let nodeResults = Concurrent.ConcurrentDictionary<string, GraphDef.NodeAction>()
    let nodes = Concurrent.ConcurrentDictionary<string, GraphDef.Node>()
    let scheduledNodeStatus = Concurrent.ConcurrentDictionary<string, bool>()
    let hub = Hub.Create(options.MaxConcurrency)

    let getNodeAction (node: GraphDef.Node) hasChildRebuilding =
        // task is forced to build
        if node.Action = GraphDef.NodeAction.Build then
            Log.Debug("{NodeId} is mark for build", node.Id)
            (GraphDef.NodeAction.Build, DateTime.MaxValue)

        // child task is building (upward cascading)
        elif hasChildRebuilding then
            Log.Debug("{NodeId} must rebuild because child is rebuilding", node.Id)
            (GraphDef.NodeAction.Build, DateTime.MaxValue)

        // cache related rules
        elif node.Cache <> GraphDef.Cacheability.Never then
            let useRemote = GraphDef.isRemoteCacheable options node
            let cacheEntryId = GraphDef.buildCacheKey node
            match cache.TryGetSummaryOnly useRemote cacheEntryId with
            | Some (_, summary) ->
                Log.Debug("{NodeId} has existing build summary", node.Id)

                // retry requested and task is failed
                if options.Retry && (not summary.IsSuccessful) then
                    Log.Debug("{NodeId} must rebuild because retry requested and node is failed", node.Id)
                    (GraphDef.NodeAction.Build, DateTime.MaxValue)
                // task is failed but restorable - ensure it's reported as failed
                elif not summary.IsSuccessful then
                    Log.Debug("{NodeId} must restore as failed", node.Id)
                    (GraphDef.NodeAction.Summary, summary.EndedAt)
                // task is cached
                elif node.Cache = GraphDef.Cacheability.External then
                    Log.Debug("{NodeId} is external {Date}", node.Id, summary.EndedAt)
                    (GraphDef.NodeAction.Ignore, summary.EndedAt)
                else
                    Log.Debug("{NodeId} is restorable {Date}", node.Id, summary.EndedAt)
                    (GraphDef.NodeAction.Restore, summary.EndedAt)
            | _ ->
                Log.Debug("{NodeId} must be built since no summary and required", node.Id)
                (GraphDef.NodeAction.Build, DateTime.MaxValue)

        // not cacheable
        else
            Log.Debug("{NodeId} is not cacheable", node.Id)
            (GraphDef.NodeAction.Build, DateTime.MaxValue)


    let rec scheduleNodeAction nodeId =
        if scheduledNodeStatus.TryAdd(nodeId, true) then
            let node = graph.Nodes[nodeId]

            // get the status of dependencies
            let dependencyStatus =
                node.Dependencies
                |> Seq.map (fun projectId ->
                    scheduleNodeAction projectId
                    hub.GetSignal<DateTime> projectId)
                |> List.ofSeq
            hub.SubscribeBackground $"{nodeId} status" dependencyStatus (fun () ->
                let hasChildRebuilding = node.Dependencies |> Seq.exists (fun projectId -> nodeResults[projectId].IsBuild)
                let (buildRequest, buildDate) = getNodeAction node hasChildRebuilding

                // skip ignore nodes on dependencies
                let actionableDependencies =
                    node.Dependencies
                    |> Set.filter (fun depId -> nodes[depId].Action <> GraphDef.NodeAction.Ignore)

                let updatedNode =
                    { node with
                        GraphDef.Node.Dependencies = actionableDependencies
                        GraphDef.Node.Action = buildRequest }

                nodes.TryAdd(nodeId, updatedNode) |> ignore
                nodeResults.TryAdd(nodeId, buildRequest) |> ignore

                let nodeStatusSignal = hub.GetSignal<DateTime> nodeId
                nodeStatusSignal.Set buildDate)

    graph.RootNodes |> Seq.iter scheduleNodeAction

    let status = hub.WaitCompletion()
    match status with
    | Status.Ok ->
        Log.Debug("NodeStateEvaluator successful")
    | Status.UnfulfilledSubscription (subscription, signals) ->
        let unraisedSignals = signals |> String.join ","
        Log.Fatal($"NodeStateEvaluator '{subscription}' has pending operations on '{unraisedSignals}'")
    | Status.SubscriptionError edi ->
        forwardExternalError("BuiNodeStateEvaluatorld failed", edi.SourceException)

    let nodes =
        graph.Nodes
        |> Map.addMap (nodes |> Seq.map (|KeyValue|) |> Map.ofSeq)

    let rootNodes =
        graph.RootNodes
        |> Set.filter (fun nodeId ->
            match nodes[nodeId].Action with
            | GraphDef.NodeAction.Build | GraphDef.NodeAction.Summary -> true
            | _ -> false)

    let graph =
        { graph with
            GraphDef.Graph.Nodes = nodes
            GraphDef.Graph.RootNodes = rootNodes }
    graph
