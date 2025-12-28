
module GraphPipeline.Action

open System
open Collections
open System.Collections.Concurrent
open Serilog
open Terrabuild.PubSub
open Errors
open GraphDef

let build (options: ConfigOptions.Options) (cache: Cache.ICache) (graph: Graph) =
    let nodes = ConcurrentDictionary<string, Node>()
    let scheduledNodeStatus = ConcurrentDictionary<string, bool>()
    use hub = Hub.Create(options.MaxConcurrency)

    let getNodeAction (node: Node) hasChildBuilding =
        // task is forced to build
        if node.Build = BuildMode.Always then
            Log.Debug("{NodeId} is marked for build", node.Id)
            (RunAction.Exec, DateTime.MaxValue)

        // child task is building (upward cascading)
        elif hasChildBuilding then
            Log.Debug("{NodeId} must build because child is building", node.Id)
            (RunAction.Exec, DateTime.MaxValue)

        // cache related rules
        elif node.Artifacts <> ArtifactMode.None then
            let useRemote = isRemoteCacheable options node
            let cacheEntryId = buildCacheKey node
            match cache.TryGetSummaryOnly useRemote cacheEntryId with
            | Some (_, summary) ->
                Log.Debug("{NodeId} has existing build summary", node.Id)

                // retry requested and task is failed
                if options.Retry && (not summary.IsSuccessful) then
                    Log.Debug("{NodeId} must build because retry requested and node is failed", node.Id)
                    (RunAction.Exec, DateTime.MaxValue)
                // task is failed but restorable - ensure it's reported as failed
                elif not summary.IsSuccessful then
                    Log.Debug("{NodeId} must restore as failed", node.Id)
                    (RunAction.Summary, summary.EndedAt)
                // task is cached
                elif node.Artifacts = ArtifactMode.External then
                    Log.Debug("{NodeId} is external {Date}", node.Id, summary.EndedAt)
                    (RunAction.Summary, summary.EndedAt)
                else
                    Log.Debug("{NodeId} is restorable {Date}", node.Id, summary.EndedAt)
                    (RunAction.Restore, summary.EndedAt)
            | _ ->
                Log.Debug("{NodeId} has no summary and must build", node.Id)
                (RunAction.Exec, DateTime.MaxValue)

        // not cacheable
        else
            Log.Debug("{NodeId} is not cacheable", node.Id)
            (RunAction.Exec, DateTime.MaxValue)


    let rec scheduleNodeAction nodeId =
        if scheduledNodeStatus.TryAdd(nodeId, true) then
            let targetNode = graph.Nodes[nodeId]

            // get the status of dependencies
            let dependencyStatus =
                targetNode.Dependencies
                |> Seq.map (fun projectId ->
                    scheduleNodeAction projectId
                    hub.GetSignal<DateTime> projectId)
                |> List.ofSeq
            hub.SubscribeBackground $"{nodeId} status" dependencyStatus (fun () ->
                let hasChildBuilding = targetNode.Dependencies |> Seq.exists (fun projectId -> 
                    let node = nodes[projectId]
                    node.Action = RunAction.Exec && node.Build <> BuildMode.Lazy)
                let nodeAction, buildDate = getNodeAction targetNode hasChildBuilding
                let targetNode = { targetNode with Action = nodeAction }
                nodes.TryAdd(targetNode.Id, targetNode) |> ignore
                hub.GetSignal<DateTime>(targetNode.Id).Set(buildDate))

    graph.RootNodes |> Seq.iter scheduleNodeAction

    let status = hub.WaitCompletion()
    match status with
    | Status.Ok ->
        Log.Debug("NodeStateEvaluator successful")
    | Status.UnfulfilledSubscription (subscription, signals) ->
        let unraisedSignals = signals |> String.join ","
        Log.Fatal("NodeStateEvaluator '{Subscription}' has pending operations on '{UnraisedSignals}'", subscription, unraisedSignals)
    | Status.SubscriptionError edi ->
        forwardInvalidArg("Failed to compute actions", edi.SourceException)

    let mutable nodes = graph.Nodes |> Map.addMap (nodes |> Seq.map (|KeyValue|) |> Map.ofSeq)
    let rootNodes =
        graph.RootNodes
        |> Set.filter (fun nodeId ->
            let node = nodes[nodeId]
            node.Action = RunAction.Exec && node.Build <> BuildMode.Lazy)

    let graph =
        { graph with
            Graph.Nodes = nodes
            Graph.RootNodes = rootNodes }
    graph
