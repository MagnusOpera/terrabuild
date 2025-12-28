module GraphPipeline.Cascade

open Collections
open GraphDef
open Serilog
open System.Collections.Generic

let build (graph: Graph) =

    let node2dependents = 
        graph.Nodes
        |> Seq.collect (fun (KeyValue(nodeId, node)) -> node.Dependencies |> Seq.map (fun depId -> depId, nodeId))
        |> Seq.groupBy fst
        |> Map.ofSeq
        |> Map.map (fun _ depIds -> depIds |> Seq.map snd |> Set.ofSeq)

    let nodes = graph.Nodes |> Dictionary<string, Node>
    let nodeRequirements = Dictionary<string, bool>()
    let rec getNodeRequirements nodeId =
        match nodeRequirements.TryGetValue(nodeId) with
        | true, requirement -> requirement
        | _ ->
            let node = nodes[nodeId]
            let isRequired =
                if node.Action = RunAction.Exec && node.Build <> BuildMode.Lazy then true
                else
                    node2dependents
                    |> Map.tryFind nodeId
                    |> Option.defaultValue Set.empty
                    |> Seq.exists getNodeRequirements

            Log.Debug("Node '{NodeId}' has requirement '{Requirement}'", node.Id, isRequired)
            nodeRequirements[nodeId] <- isRequired
            if isRequired then
                let node = { node with Required = isRequired }
                nodes[node.Id] <- node
            isRequired

    for nodeId in graph.Nodes.Keys do
        getNodeRequirements nodeId |> ignore

    { graph with Graph.Nodes = nodes |> Map.ofDict }
