module GraphPipeline.Cascade
open System.Collections
open GraphDef

let build (graph: Graph) =
    let processedNodes = Generic.Dictionary<string, bool>()
    let mutable nodes = graph.Nodes

    let rec propagate nodeId =
        if processedNodes.TryAdd(nodeId, true) then
            let mutable node = graph.Nodes[nodeId]
            let build =
                match node.Action, node.Rebuild with
                | NodeAction.Build, _ -> true
                | NodeAction.Restore, Rebuild.Cascade ->
                    node <- { node with Action = NodeAction.Build; Cache = Cacheability.Local }
                    true
                | _ ->
                    false

            if build then
                nodes <- nodes |> Map.add nodeId node
                for dependency in node.Dependencies do
                    propagate dependency

    for rootNode in graph.RootNodes do
        propagate rootNode

    let graph = { graph with GraphDef.Graph.Nodes = nodes }
    graph
