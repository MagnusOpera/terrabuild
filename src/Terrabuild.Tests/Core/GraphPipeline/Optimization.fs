module Terrabuild.Tests.Core.GraphPipeline.Optimization

open System
open System.Collections.Generic
open System.Diagnostics
open FsUnit
open NUnit.Framework
open GraphDef

let private buildNode id dependencies phase action artifacts build required =
    { Node.Id = id
      Node.ProjectId = id
      Node.ProjectName = None
      Node.ProjectDir = id
      Node.Target = "build"
      Node.Phase = phase
      Node.Locks = Set.empty
      Node.Dependencies = dependencies
      Node.PhaseDependencies = Set.empty
      Node.Outputs = Set.empty
      Node.ProjectHash = id
      Node.TargetHash = id
      Node.ClusterHash = None
      Node.Operations = []
      Node.EvaluationInputs = []
      Node.EnvironmentSensitive = None
      Node.Artifacts = artifacts
      Node.Build = build
      Node.Batch = BatchMode.Never
      Node.Action = action
      Node.Required = required }

let private buildGraph (nodes: Node seq) phases =
    let nodes = nodes |> Seq.map (fun node -> node.Id, node) |> Map.ofSeq
    let allNodeIds = nodes.Keys |> Set.ofSeq
    let dependencyIds = nodes.Values |> Seq.collect _.Dependencies |> Set.ofSeq
    { Graph.Nodes = nodes
      Graph.RootNodes = allNodeIds - dependencyIds
      Graph.Batches = Map.empty
      Graph.Phases = phases }

let private legacyPhaseBuild (graph: Graph) =
    let memo = Dictionary<string, Set<string>>()
    let rec collect phaseName =
        match memo.TryGetValue phaseName with
        | true, dependencies -> dependencies
        | _ ->
            let direct = graph.Phases |> Map.tryFind phaseName |> Option.defaultValue Set.empty
            let dependencies = direct + (direct |> Seq.collect collect |> Set.ofSeq)
            memo[phaseName] <- dependencies
            dependencies

    let phaseDependencies = graph.Phases |> Map.map (fun phaseName _ -> collect phaseName)
    let phaseNodes =
        graph.Nodes.Values
        |> Seq.choose (fun node -> node.Phase |> Option.map (fun phase -> phase, node.Id))
        |> Seq.groupBy fst
        |> Seq.map (fun (phase, nodes) -> phase, nodes |> Seq.map snd |> Set.ofSeq)
        |> Map.ofSeq

    let nodes =
        graph.Nodes
        |> Map.map (fun _ node ->
            let dependencies =
                node.Phase
                |> Option.bind (fun phase -> phaseDependencies |> Map.tryFind phase)
                |> Option.defaultValue Set.empty
                |> Seq.collect (fun phase -> phaseNodes |> Map.tryFind phase |> Option.defaultValue Set.empty)
                |> Set.ofSeq
                |> Set.filter (fun dependency -> node.Dependencies |> Set.contains dependency |> not)
            { node with
                Dependencies = node.Dependencies + dependencies
                PhaseDependencies = dependencies })

    let mutable visited = Set.empty<string>
    let rec visit path nodeId =
        if path |> List.contains nodeId then
            failwith "Unexpected cycle in benchmark graph"
        elif visited |> Set.contains nodeId |> not then
            let path = nodeId :: path
            nodes[nodeId].Dependencies |> Set.iter (visit path)
            visited <- visited |> Set.add nodeId
    nodes.Keys |> Seq.iter (visit [])

    let allNodeIds = nodes.Keys |> Set.ofSeq
    let dependencyIds = nodes.Values |> Seq.collect _.Dependencies |> Set.ofSeq
    { graph with Nodes = nodes; RootNodes = allNodeIds - dependencyIds }

let private legacyCascadeBuild (graph: Graph) =
    let node2dependents =
        graph.Nodes
        |> Seq.collect (fun (KeyValue(nodeId, node)) -> node.Dependencies |> Seq.map (fun dependencyId -> dependencyId, nodeId))
        |> Seq.groupBy fst
        |> Map.ofSeq
        |> Map.map (fun _ items -> items |> Seq.map snd |> Set.ofSeq)

    let nodes = Dictionary<string, Node>(graph.Nodes)
    let requirements = Dictionary<string, bool>()
    let rec required nodeId =
        match requirements.TryGetValue nodeId with
        | true, value -> value
        | _ ->
            let node = nodes[nodeId]
            let requiredDependents = lazy (
                node2dependents
                |> Map.tryFind nodeId
                |> Option.defaultValue Set.empty
                |> Seq.filter (fun dependentId ->
                    let dependent = nodes[dependentId]
                    dependent.Action = RunAction.Exec && required dependentId)
                |> Seq.sort
                |> List.ofSeq)
            let value =
                match node with
                | { Required = true } -> true
                | { Action = RunAction.Ignore } -> false
                | { Action = RunAction.Restore; Artifacts = ArtifactMode.External } -> false
                | { Action = RunAction.Exec } when node.Build <> BuildMode.Lazy -> true
                | _ -> requiredDependents.Value <> []
            requirements[nodeId] <- value
            if node.Required <> value then nodes[nodeId] <- { node with Required = value }
            value

    for nodeId in graph.Nodes.Keys do required nodeId |> ignore
    { graph with Nodes = nodes |> Seq.map (|KeyValue|) |> Map.ofSeq }

let private pick (values: 'a array) index = values[index % values.Length]

let private measureMilliseconds iterations action =
    [ for _ in 1..iterations do
        GC.Collect()
        GC.WaitForPendingFinalizers()
        GC.Collect()
        let stopwatch = Stopwatch.StartNew()
        action () |> ignore
        stopwatch.Stop()
        yield stopwatch.Elapsed.TotalMilliseconds ]
    |> List.min

[<Test>]
let ``topology preserves resolver DFS order and indexes sorted dependents`` () =
    let node id dependencies =
        buildNode id dependencies None RunAction.Ignore ArtifactMode.Managed BuildMode.Auto false
    let graph =
        buildGraph
            [ node "A" (Set [ "B"; "C" ])
              node "B" (Set [ "C" ])
              node "C" Set.empty
              node "D" Set.empty ]
            Map.empty

    let topology = GraphPipeline.Topology.build graph
    topology.DependencyFirst |> should equal [| "C"; "B"; "A"; "D" |]
    topology.Dependents["C"] |> should equal [| "A"; "B" |]

[<Test>]
let ``optimized phase lowering matches legacy lowering`` () =
    let phases = Map [ "phase-0", Set.empty; "phase-1", Set [ "phase-0" ]; "phase-2", Set [ "phase-1" ] ]
    let nodes =
        [ for index in 0..119 do
            let phaseIndex = index / 40
            let dependencies =
                [ for dependencyIndex in 0..index-1 do
                    if (index * 17 + dependencyIndex * 13) % 19 = 0 then
                        yield $"node-{dependencyIndex:D3}" ]
                |> Set.ofList
            yield
                buildNode
                    $"node-{index:D3}"
                    dependencies
                    (Some $"phase-{phaseIndex}")
                    RunAction.Ignore
                    ArtifactMode.Managed
                    BuildMode.Auto
                    false ]
    let graph = buildGraph nodes phases

    GraphPipeline.Phase.build graph |> should equal (legacyPhaseBuild graph)

[<Test>]
let ``optimized cascade matches legacy decisions across generated DAGs`` () =
    let actions = [| RunAction.Ignore; RunAction.Summary; RunAction.Restore; RunAction.Exec |]
    let artifacts = [| ArtifactMode.None; ArtifactMode.Workspace; ArtifactMode.Managed; ArtifactMode.External |]
    let builds = [| BuildMode.Lazy; BuildMode.Auto; BuildMode.Always |]

    for seed in 0..31 do
        let nodes =
            [ for index in 0..63 do
                let dependencies =
                    [ for dependencyIndex in 0..index-1 do
                        if (seed * 23 + index * 17 + dependencyIndex * 11) % 29 = 0 then
                            yield $"node-{dependencyIndex:D3}" ]
                    |> Set.ofList
                yield
                    buildNode
                        $"node-{index:D3}"
                        dependencies
                        None
                        (pick actions (seed + index))
                        (pick artifacts (seed * 3 + index))
                        (pick builds (seed * 5 + index))
                        ((seed + index * 7) % 17 = 0) ]
        let graph = buildGraph nodes Map.empty

        GraphPipeline.Cascade.build graph |> should equal (legacyCascadeBuild graph)

[<Test; Explicit("Graph pipeline performance benchmark")>]
let ``optimized phase lowering benchmark`` () =
    let phaseCount = 6
    let nodesPerPhase = 150
    let phases =
        [ for phaseIndex in 0..phaseCount-1 ->
            $"phase-{phaseIndex}",
            if phaseIndex = 0 then Set.empty else Set [ $"phase-{phaseIndex - 1}" ] ]
        |> Map.ofList
    let nodes =
        [ for phaseIndex in 0..phaseCount-1 do
            for nodeIndex in 0..nodesPerPhase-1 do
                let id = $"node-{phaseIndex:D2}-{nodeIndex:D4}"
                yield
                    buildNode id Set.empty (Some $"phase-{phaseIndex}") RunAction.Ignore
                        ArtifactMode.Managed BuildMode.Auto false ]
    let graph = buildGraph nodes phases

    // Warm both paths before collecting comparable timings.
    legacyPhaseBuild graph |> ignore
    GraphPipeline.Phase.build graph |> ignore
    let legacyMs = measureMilliseconds 3 (fun () -> legacyPhaseBuild graph)
    let optimizedMs = measureMilliseconds 3 (fun () -> GraphPipeline.Phase.build graph)
    TestContext.Progress.WriteLine($"phase lowering: legacy={legacyMs:F1}ms optimized={optimizedMs:F1}ms")
    optimizedMs |> should be (lessThan legacyMs)

[<Test; Explicit("Graph pipeline performance benchmark")>]
let ``optimized cascade benchmark`` () =
    let nodes =
        [ for index in 0..4999 do
            let dependencies =
                if index % 50 = 0 then Set.empty
                else Set [ $"node-{index - 1:D5}" ]
            let action = if index % 50 = 49 then RunAction.Exec else RunAction.Summary
            yield
                buildNode $"node-{index:D5}" dependencies None action
                    ArtifactMode.Managed BuildMode.Auto false ]
    let graph = buildGraph nodes Map.empty

    legacyCascadeBuild graph |> ignore
    GraphPipeline.Cascade.build graph |> ignore
    let legacyMs = measureMilliseconds 3 (fun () -> legacyCascadeBuild graph)
    let optimizedMs = measureMilliseconds 3 (fun () -> GraphPipeline.Cascade.build graph)
    TestContext.Progress.WriteLine($"cascade: legacy={legacyMs:F1}ms optimized={optimizedMs:F1}ms")
    optimizedMs |> should be (lessThan legacyMs)
