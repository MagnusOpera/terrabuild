module GraphPipeline.Cascade

open Collections
open GraphDef

let build (graph: Graph) =

    let node2dependents = 
        graph.Nodes
        |> Seq.collect (fun (KeyValue(nodeId, node)) -> node.Dependencies |> Seq.map (fun depId -> depId, nodeId))
        |> Seq.groupBy fst
        |> Map.ofSeq
        |> Map.map (fun _ depIds -> depIds |> Seq.map snd |> Set.ofSeq)

    let leafNodes = graph.Nodes |> Map.filter (fun _ node -> node.Dependencies |> Set.isEmpty)

    let mutable nodes = graph.Nodes

    let mutable nodeRequirements = Map.empty
    let rec getNodeRequirements nodeId =
        match nodeRequirements |> Map.tryFind nodeId with
        | Some requirement -> requirement
        | _ ->
            let node = nodes[nodeId]
            let isRequired =
                if node.Required then node.Required
                else
                    node2dependents
                    |> Map.tryFind nodeId
                    |> Option.map (Seq.exists getNodeRequirements)
                    |> Option.defaultValue false

            nodeRequirements <- nodeRequirements |> Map.add nodeId isRequired
            let node = { node with Required = isRequired }
            nodes <- nodes |> Map.add node.Id node
            isRequired

    leafNodes.Keys |> Seq.iter (fun leafNodeId -> getNodeRequirements leafNodeId |> ignore)

    { graph with Graph.Nodes = nodes }
