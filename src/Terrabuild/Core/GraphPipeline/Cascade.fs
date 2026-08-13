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
            let requiredDependents =
                lazy (
                    node2dependents
                    |> Map.tryFind nodeId
                    |> Option.defaultValue Set.empty
                    |> Seq.filter getNodeRequirements
                    |> Seq.sort
                    |> List.ofSeq)
            let isRequired =
                match node with
                | { Required = true } -> true
                | { Action = RunAction.Ignore } -> false
                | { Action = RunAction.Restore; Artifacts = ArtifactMode.External } -> false
                | { Action = RunAction.Exec } when node.Build <> BuildMode.Lazy -> true
                | _ -> requiredDependents.Value <> []

            let dependents =
                if requiredDependents.IsValueCreated then requiredDependents.Value
                else []
            let reason =
                match node with
                | { Required = true } -> "already-required"
                | { Action = RunAction.Ignore } -> "ignored"
                | { Action = RunAction.Restore; Artifacts = ArtifactMode.External } -> "external-restore"
                | { Action = RunAction.Exec } when node.Build <> BuildMode.Lazy -> "executing"
                | _ when dependents <> [] -> "required-by-dependent"
                | _ -> "not-required"
            DiagnosticsTelemetry.recordRequirement {
                DiagnosticsTelemetry.RequirementDecision.NodeId = node.Id
                Required = isRequired
                Reason = reason
                Dependents = dependents
            }
            nodeRequirements[nodeId] <- isRequired
            if node.Required <> isRequired then
                let node = { node with Required = isRequired }
                nodes[node.Id] <- node
            isRequired

    for nodeId in graph.Nodes.Keys do
        getNodeRequirements nodeId |> ignore

    { graph with Graph.Nodes = nodes |> Map.ofDict }
