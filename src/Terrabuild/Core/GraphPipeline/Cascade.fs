module GraphPipeline.Cascade

open System.Collections.Generic
open Collections
open GraphDef
open Serilog

let build (graph: Graph) =
    // For each node: have we already processed it as "required"?
    // false means "seen but only processed as not-required"
    let processedRequired = Dictionary<string, bool>()
    let mutable requiredNodes = Set.empty

    let rec propagate parentIsRequired nodeId =
        let node = graph.Nodes[nodeId]

        if node.Action <> RunAction.Ignore then
            let nodeRequired = parentIsRequired || node.Action = RunAction.Exec

            match processedRequired.TryGetValue(nodeId) with
            | true, alreadyRequired when alreadyRequired || not nodeRequired ->
                // Already processed with >= this requiredness
                ()
            | _ ->
                // Either first time, or upgrade from not-required -> required
                processedRequired[nodeId] <- nodeRequired
                if nodeRequired then
                    Log.Debug("Node {NodeId} is marked as required", nodeId)
                    requiredNodes <- requiredNodes |> Set.add node.Id
                node.Dependencies |> Seq.iter (propagate nodeRequired)

    graph.RootNodes |> Seq.iter (propagate false)

    // keep only runnable root nodes
    let rootNodes =
        graph.RootNodes
        |> Set.filter (fun rootNodeId -> requiredNodes |> Set.contains rootNodeId)

    { graph with Graph.RootNodes = rootNodes }
