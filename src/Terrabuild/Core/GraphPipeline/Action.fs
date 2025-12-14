
module GraphPipeline.Action


open System
open Collections
open Serilog
open Terrabuild.PubSub
open Errors
open GraphDef

let build (options: ConfigOptions.Options) (cache: Cache.ICache) (graph: Graph) =
    let nodeResults = Concurrent.ConcurrentDictionary<string, NodeAction>()
    let nodes = Concurrent.ConcurrentDictionary<string, Node>()
    let scheduledNodeStatus = Concurrent.ConcurrentDictionary<string, bool>()
    let hub = Hub.Create(options.MaxConcurrency)

    // member node id -> batch id
    let memberToBatch =
        graph.Batches
        |> Seq.collect (fun (KeyValue(batchId, members)) ->
            members |> Seq.map (fun nodeId -> nodeId, batchId))
        |> Map.ofSeq

    let execId (nodeId: string) =
        memberToBatch |> Map.tryFind nodeId |> Option.defaultValue nodeId

    let getNodeAction (node: Node) hasChildBuilding =
        // task is forced to build
        if node.Action = NodeAction.Build then
            Log.Debug("{NodeId} is marked for build", node.Id)
            (NodeAction.Build, DateTime.MaxValue)

        // child task is building (upward cascading)
        elif hasChildBuilding then
            Log.Debug("{NodeId} must build because child is building", node.Id)
            (NodeAction.Build, DateTime.MaxValue)

        // cache related rules
        elif node.Artifacts <> Artifacts.None then
            let useRemote = isRemoteCacheable options node
            let cacheEntryId = buildCacheKey node
            match cache.TryGetSummaryOnly useRemote cacheEntryId with
            | Some (_, summary) ->
                Log.Debug("{NodeId} has existing build summary", node.Id)

                // retry requested and task is failed
                if options.Retry && (not summary.IsSuccessful) then
                    Log.Debug("{NodeId} must build because retry requested and node is failed", node.Id)
                    (NodeAction.Build, DateTime.MaxValue)
                // task is failed but restorable - ensure it's reported as failed
                elif not summary.IsSuccessful then
                    Log.Debug("{NodeId} must restore as failed", node.Id)
                    (NodeAction.Summary, summary.EndedAt)
                // task is cached
                elif node.Artifacts = Artifacts.External then
                    Log.Debug("{NodeId} is external {Date}", node.Id, summary.EndedAt)
                    (NodeAction.Summary, summary.EndedAt)
                else
                    Log.Debug("{NodeId} is restorable {Date}", node.Id, summary.EndedAt)
                    (NodeAction.Restore, summary.EndedAt)
            | _ ->
                Log.Debug("{NodeId} must be built since no summary and required", node.Id)
                (NodeAction.Build, DateTime.MaxValue)

        // not cacheable
        else
            Log.Debug("{NodeId} is not cacheable", node.Id)
            (NodeAction.Build, DateTime.MaxValue)


    let rec scheduleNodeAction nodeId =
        let id = execId nodeId
        if scheduledNodeStatus.TryAdd(id, true) then
            let targetNode = graph.Nodes[id]
            let membersOpt = graph.Batches |> Map.tryFind id

            // get the status of dependencies
            let dependencyStatus =
                targetNode.Dependencies
                |> Seq.map (fun projectId ->
                    scheduleNodeAction projectId
                    hub.GetSignal<DateTime> projectId)
                |> List.ofSeq
            hub.SubscribeBackground $"{id} status" dependencyStatus (fun () ->
                let hasChildBuilding = targetNode.Dependencies |> Seq.exists (fun projectId -> nodeResults[projectId].IsBuild)
                match membersOpt with
                | Some members ->
                    let rec computeMemberBuildRequest currAction currDate members =
                        match currAction, members with
                        | NodeAction.Build, _ -> currAction, currDate
                        | _, [] -> currAction, currDate
                        | _, memberId :: tail ->
                            let (newAction, newDate) = getNodeAction graph.Nodes[memberId] hasChildBuilding
                            let (newAction, newDate) =
                                if currAction < newAction then (newAction, max currDate newDate)
                                else (currAction, currDate)
                            computeMemberBuildRequest newAction newDate tail
                    let memberBuildRequest, memberBuildDate =
                        computeMemberBuildRequest NodeAction.Ignore DateTime.MinValue (members |> List.ofSeq)
                    for batchMember in members do
                        let memberNode = { graph.Nodes[batchMember] with Action = memberBuildRequest }
                        nodes.TryAdd(memberNode.Id, memberNode) |> ignore
                        nodeResults.TryAdd(batchMember, memberBuildRequest) |> ignore
                        hub.GetSignal<DateTime>(batchMember).Set(memberBuildDate)
                    let targetNode = { targetNode with Action = memberBuildRequest }
                    nodes.TryAdd(targetNode.Id, targetNode) |> ignore
                    nodeResults.TryAdd(targetNode.Id, memberBuildRequest) |> ignore
                | _ ->
                    let nodeAction, buildDate = getNodeAction targetNode hasChildBuilding
                    let targetNode = { targetNode with Action = nodeAction }
                    nodes.TryAdd(targetNode.Id, targetNode) |> ignore
                    nodeResults.TryAdd(targetNode.Id, nodeAction) |> ignore
                    hub.GetSignal<DateTime>(targetNode.Id).Set(buildDate))

    graph.RootNodes |> Seq.iter scheduleNodeAction

    let status = hub.WaitCompletion()
    match status with
    | Status.Ok ->
        Log.Debug("NodeStateEvaluator successful")
    | Status.UnfulfilledSubscription (subscription, signals) ->
        let unraisedSignals = signals |> String.join ","
        Log.Fatal($"NodeStateEvaluator '{subscription}' has pending operations on '{unraisedSignals}'")
    | Status.SubscriptionError edi ->
        forwardInvalidArg("Failed to compute actions", edi.SourceException)

    let nodes =
        graph.Nodes
        |> Map.addMap (nodes |> Seq.map (|KeyValue|) |> Map.ofSeq)

    let rootNodes =
        graph.RootNodes
        |> Set.filter (fun nodeId ->
            match nodes[nodeId].Action with
            | NodeAction.Ignore
            | NodeAction.Restore -> false
            | _ -> true)

    let graph =
        { graph with
            Graph.Nodes = nodes
            Graph.RootNodes = rootNodes }
    graph
