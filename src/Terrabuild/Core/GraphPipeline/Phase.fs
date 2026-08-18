module GraphPipeline.Phase

open System.Collections.Generic
open Collections
open Errors
open GraphDef

let private transitiveDependencies (phases: Map<string, Set<string>>) =
    let memo = Dictionary<string, Set<string>>()
    let rec collect phaseName =
        match memo.TryGetValue phaseName with
        | true, dependencies -> dependencies
        | _ ->
            let direct = phases |> Map.tryFind phaseName |> Option.defaultValue Set.empty
            let dependencies = direct + (direct |> Seq.collect collect |> Set.ofSeq)
            memo[phaseName] <- dependencies
            dependencies

    phases |> Map.map (fun phaseName _ -> collect phaseName)

let private validateCombinedGraph (nodes: Map<string, Node>) =
    let visited = HashSet<string>()
    let active = HashSet<string>()
    let rec visit path nodeId =
        if active.Contains nodeId then
            let cycle = nodeId :: path |> List.rev |> String.join " -> "
            raiseInvalidArg $"Circular target dependency detected after applying phases: {cycle}"
        elif visited.Contains nodeId |> not then
            active.Add nodeId |> ignore
            let path = nodeId :: path
            nodes[nodeId].Dependencies |> Set.iter (visit path)
            active.Remove nodeId |> ignore
            visited.Add nodeId |> ignore

    nodes |> Map.keys |> Seq.iter (visit [])

let build (graph: Graph) =
    let phaseDependencies = transitiveDependencies graph.Phases
    let phaseNodes =
        graph.Nodes
        |> Map.values
        |> Seq.choose (fun node -> node.Phase |> Option.map (fun phase -> phase, node.Id))
        |> Seq.groupBy fst
        |> Seq.map (fun (phase, nodes) -> phase, nodes |> Seq.map snd |> Set.ofSeq)
        |> Map.ofSeq

    // All nodes in the same phase have the same phase-level dependency set.
    // Expand it once per phase rather than once per node.
    let expandedPhaseNodes =
        phaseDependencies
        |> Map.map (fun _ dependencies ->
            dependencies
            |> Seq.collect (fun phase -> phaseNodes |> Map.tryFind phase |> Option.defaultValue Set.empty)
            |> Set.ofSeq)

    let nodes =
        graph.Nodes
        |> Map.map (fun _ node ->
            let phaseDependencies =
                node.Phase
                |> Option.bind (fun phase -> expandedPhaseNodes |> Map.tryFind phase)
                |> Option.defaultValue Set.empty
                |> fun dependencies -> dependencies - node.Dependencies
            { node with
                Dependencies = node.Dependencies + phaseDependencies
                PhaseDependencies = phaseDependencies })

    validateCombinedGraph nodes

    let allNodeIds = nodes |> Map.keys |> Set.ofSeq
    let dependencyIds = nodes |> Map.values |> Seq.collect _.Dependencies |> Set.ofSeq
    { graph with
        Nodes = nodes
        RootNodes = allNodeIds - dependencyIds }
