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
    let mutable visited = Set.empty<string>
    let rec visit path nodeId =
        if path |> List.contains nodeId then
            let cycle = nodeId :: path |> List.rev |> String.join " -> "
            raiseInvalidArg $"Circular target dependency detected after applying phases: {cycle}"
        elif visited |> Set.contains nodeId |> not then
            let path = nodeId :: path
            nodes[nodeId].Dependencies |> Set.iter (visit path)
            visited <- visited |> Set.add nodeId

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

    let nodes =
        graph.Nodes
        |> Map.map (fun _ node ->
            let phaseDependencies =
                node.Phase
                |> Option.bind (fun phase -> phaseDependencies |> Map.tryFind phase)
                |> Option.defaultValue Set.empty
                |> Seq.collect (fun phase -> phaseNodes |> Map.tryFind phase |> Option.defaultValue Set.empty)
                |> Set.ofSeq
                |> Set.filter (fun dependency -> node.Dependencies |> Set.contains dependency |> not)
            { node with
                Dependencies = node.Dependencies + phaseDependencies
                PhaseDependencies = phaseDependencies })

    validateCombinedGraph nodes

    let allNodeIds = nodes |> Map.keys |> Set.ofSeq
    let dependencyIds = nodes |> Map.values |> Seq.collect _.Dependencies |> Set.ofSeq
    { graph with
        Nodes = nodes
        RootNodes = allNodeIds - dependencyIds }
