module GraphPipeline.Cascade

open Collections
open GraphDef
open System.Collections.Generic

let build (graph: Graph) =
    let topology = Topology.build graph
    let nodes = graph.Nodes |> Dictionary<string, Node>
    let nodeRequirements = Dictionary<string, bool>()

    // Requiredness flows from dependents to dependencies, so evaluate the
    // dependency-first order backwards. This replaces recursive memoization
    // with one explicit linear pass.
    for index = topology.DependencyFirst.Length - 1 downto 0 do
        let nodeId = topology.DependencyFirst[index]
        let node = nodes[nodeId]
        let mutable requiredDependents = []

        let isRequired, reason =
            match node with
            | { Required = true } -> true, "already-required"
            | { Action = RunAction.Ignore } -> false, "ignored"
            | { Action = RunAction.Restore; Artifacts = ArtifactMode.External } -> false, "external-restore"
            | { Action = RunAction.Exec } when node.Build <> BuildMode.Lazy -> true, "executing"
            | _ ->
                requiredDependents <-
                    topology.Dependents[nodeId]
                    |> Array.filter (fun dependentId ->
                        let dependent = nodes[dependentId]
                        dependent.Action = RunAction.Exec && nodeRequirements[dependentId])
                    |> List.ofArray
                if requiredDependents <> [] then true, "required-by-dependent"
                else false, "not-required"

        DiagnosticsTelemetry.recordRequirement {
            DiagnosticsTelemetry.RequirementDecision.NodeId = node.Id
            Required = isRequired
            Reason = reason
            Dependents = requiredDependents
        }
        nodeRequirements[nodeId] <- isRequired
        if node.Required <> isRequired then
            nodes[node.Id] <- { node with Required = isRequired }

    { graph with Graph.Nodes = nodes |> Map.ofDict }
