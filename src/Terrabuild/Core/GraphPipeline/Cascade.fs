module GraphPipeline.Cascade

open Collections
open GraphDef
open Serilog

let build (graph: Graph) =

    let node2dependents = 
        graph.Nodes
        |> Seq.collect (fun (KeyValue(nodeId, node)) -> node.Dependencies |> Seq.map (fun depId -> depId, nodeId))
        |> Seq.groupBy fst
        |> Map.ofSeq
        |> Map.map (fun _ depIds -> depIds |> Seq.map snd |> Set.ofSeq)

    let mutable nodes = graph.Nodes

    let mutable nodeRequirements = Map.empty
    let rec getNodeRequirements nodeId =
        match nodeRequirements |> Map.tryFind nodeId with
        | Some requirement -> requirement
        | _ ->
            let node = nodes[nodeId]
            let isRequired =
                if node.Required then
                    node.Required
                else
                    node2dependents
                    |> Map.tryFind nodeId
                    |> Option.defaultValue Set.empty
                    |> Seq.exists getNodeRequirements

            Log.Debug("Node {NodeId} has requirement {Requirement}", node.Id, isRequired)
            nodeRequirements <- nodeRequirements |> Map.add nodeId isRequired
            let node = { node with Required = isRequired }
            nodes <- nodes |> Map.add node.Id node
            isRequired

    for nodeId in graph.Nodes.Keys do
        getNodeRequirements nodeId |> ignore

    { graph with Graph.Nodes = nodes }
