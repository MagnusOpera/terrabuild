
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

    let getNodeAction (node: Node) (buildingDependencies: string list) =
        let cacheScope =
            if isRemoteCacheable options node then "local-and-remote"
            else "local"

        let record action reason dependencies cache =
            DiagnosticsTelemetry.recordAction {
                DiagnosticsTelemetry.ActionDecision.NodeId = node.Id
                Action = (string action).ToLowerInvariant()
                Reason = reason
                Dependencies = dependencies
                Cache = cache
            }
            action

        let cacheEvidence lookup origin previousStatus endedAt : DiagnosticsTelemetry.CacheEvidence option =
            Some {
                Scope = cacheScope
                Key = buildCacheKey node
                Lookup = lookup
                Origin = origin
                PreviousStatus = previousStatus
                SummaryEndedAt = endedAt
            }

        // task is forced to build
        if node.Build = BuildMode.Always then
            let reason = if options.Force then "forced-cli" else "configured-always"
            (record RunAction.Exec reason [] None, DateTime.MaxValue)

        // child task is building (upward cascading)
        elif buildingDependencies <> [] then
            (record RunAction.Exec "dependency-executed" buildingDependencies None, DateTime.MaxValue)

        // cache related rules
        elif node.Artifacts <> ArtifactMode.None then
            let useRemote = isRemoteCacheable options node
            let cacheEntryId = buildCacheKey node
            match cache.TryGetSummaryOnly useRemote cacheEntryId with
            | Some (origin, summary) ->
                let origin = Some ((string origin).ToLowerInvariant())

                // retry requested and task is failed
                if options.Retry && (not summary.IsSuccessful) then
                    let cache = cacheEvidence "hit" origin (Some "failure") (Some summary.EndedAt)
                    (record RunAction.Exec "retry-failed-cache" [] cache, DateTime.MaxValue)
                // task is failed but restorable - ensure it's reported as failed
                elif not summary.IsSuccessful then
                    let cache = cacheEvidence "hit" origin (Some "failure") (Some summary.EndedAt)
                    (record RunAction.Summary "cached-failure" [] cache, summary.EndedAt)
                // task is cached
                else
                    let cache = cacheEvidence "hit" origin (Some "success") (Some summary.EndedAt)
                    (record RunAction.Restore "cache-hit" [] cache, summary.EndedAt)
            | _ ->
                let cache = cacheEvidence "miss" None None None
                (record RunAction.Exec "cache-miss" [] cache, DateTime.MaxValue)

        // not cacheable
        else
            (record RunAction.Exec "non-cacheable" [] None, DateTime.MaxValue)


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
                let buildingDependencies =
                    targetNode.Dependencies - targetNode.PhaseDependencies
                    |> Seq.filter (fun projectId ->
                        let node = nodes[projectId]
                        node.Action = RunAction.Exec && node.Build <> BuildMode.Lazy)
                    |> Seq.sort
                    |> List.ofSeq
                let nodeAction, buildDate = getNodeAction targetNode buildingDependencies
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
            match node.Action with
            | RunAction.Exec -> true
            | RunAction.Summary -> true
            | _ -> false)

    // Explicitly selected roots must run even when their dependency behavior is lazy.
    rootNodes
    |> Set.iter (fun nodeId ->
        let node = nodes[nodeId]
        if node.Action = RunAction.Exec && not node.Required then
            nodes <- nodes |> Map.add nodeId { node with Required = true })

    let graph =
        { graph with
            Graph.Nodes = nodes
            Graph.RootNodes = rootNodes }
    graph
