
module ActionBuilder


open System
open Collections
open Serilog


let build (options: ConfigOptions.Options) (cache: Cache.ICache) (graph: GraphDef.Graph) =
    let allowRemoteCache = options.LocalOnly |> not
    let mutable nodeResults = Map.empty
    let mutable nodes = Map.empty

    let computeNodeAction (node: GraphDef.Node) maxCompletionChildren =
        if node.Action = GraphDef.NodeAction.Build then
            Log.Debug("{NodeId} must rebuild because force requested", node.Id)
            (GraphDef.NodeAction.Build, DateTime.MaxValue)

        elif node.Cache <> Terrabuild.Extensibility.Cacheability.Never then
            let cacheEntryId = GraphDef.buildCacheKey node
            match cache.TryGetSummaryOnly allowRemoteCache cacheEntryId with
            | Some (_, summary) ->
                Log.Debug("{NodeId} has existing build summary", node.Id)

                // retry requested and task is failed
                if options.Retry && (not summary.IsSuccessful) then
                    Log.Debug("{NodeId} must rebuild because retry requested and node is failed", node.Id)
                    (GraphDef.NodeAction.Build, DateTime.MaxValue)

                // children are younger than task
                elif summary.EndedAt < maxCompletionChildren then
                    Log.Debug("{NodeId} must rebuild because child is rebuilding", node.Id)
                    (GraphDef.NodeAction.Build, DateTime.MaxValue)

                // task is cached
                elif node.Cache = Terrabuild.Extensibility.Cacheability.External then
                    Log.Debug("{NodeId} is external {Date}", node.Id, summary.EndedAt)
                    (GraphDef.NodeAction.Ignore, summary.EndedAt)
                else
                    Log.Debug("{NodeId} is restorable {Date}", node.Id, summary.EndedAt)
                    (GraphDef.NodeAction.Restore, summary.EndedAt)
            | _ ->
                Log.Debug("{NodeId} must be built since no summary and required", node.Id)
                (GraphDef.NodeAction.Build, DateTime.MaxValue)
        else
            Log.Debug("{NodeId} is not cacheable", node.Id)
            (GraphDef.NodeAction.Build, DateTime.MaxValue)


    let rec getNodeStatus nodeId =
        match nodeResults |> Map.tryFind nodeId with
        | Some result -> result
        | _ ->
            let node = graph.Nodes[nodeId]

            // get the status of dependencies
            let maxCompletionChildren =
                node.Dependencies
                |> Seq.map (fun projectId -> getNodeStatus projectId |> snd)
                |> Seq.maxDefault DateTime.MinValue
            let (buildRequest, buildDate) = computeNodeAction node maxCompletionChildren

            nodes <- nodes |> Map.add nodeId { node with Action = buildRequest }
            let result = (buildRequest, buildDate)
            nodeResults <- nodeResults |> Map.add nodeId result
            result

    graph.RootNodes |> Seq.iter (ignore << getNodeStatus)

    let rootNodes =
        graph.RootNodes
        |> Set.filter (fun nodeId -> nodes[nodeId].Action = GraphDef.NodeAction.Build)
    let graph =
        { graph with
            GraphDef.Graph.Nodes = nodes
            GraphDef.Graph.RootNodes = rootNodes }
    graph
