module GraphPipeline.Cascade
open System.Collections
open GraphDef

let build (graph: Graph) =
    let mutable nodes = graph.Nodes
    let exploredNodes = Generic.Dictionary<string, bool>()
    let rec propagateDownward nodeId =
        if exploredNodes.TryAdd(nodeId, true) then
            let mutable node = graph.Nodes[nodeId]
            if node.Action <> NodeAction.Ignore then
                nodes <- nodes |> Map.add node.Id node
                node.Dependencies |> Set.iter propagateDownward

    for rootNode in graph.RootNodes do
        propagateDownward rootNode

    let graph =
        { graph with
            GraphDef.Graph.Nodes = nodes }
    graph
