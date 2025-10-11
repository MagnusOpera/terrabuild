module CascadeBuilder
open System.Collections
open GraphDef

let build (graph: Graph) =
    let processedNodes = Generic.Dictionary<string, bool>()
    let nodes = Generic.Dictionary<string, Node>()

    let rec propagate nodeId =
        if processedNodes.TryAdd(nodeId, true) then
            let mutable node = graph.Nodes[nodeId]
            let build =
                match node.Action, node.Rebuild with
                | NodeAction.Build, _ -> true
                | NodeAction.Restore, Rebuild.Cascade ->
                    node <- { node with Action = NodeAction.Build }
                    true
                | _ ->
                    false

            if build then
                nodes.Add(nodeId, node)
                for dependency in node.Dependencies do
                    propagate dependency

    for rootNode in graph.RootNodes do
        propagate rootNode

    let graph = { graph with GraphDef.Graph.Nodes = nodes |> Seq.map (|KeyValue|) |> Map.ofSeq }
    graph
