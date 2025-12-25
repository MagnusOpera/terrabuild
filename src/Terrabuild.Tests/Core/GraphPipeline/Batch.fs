module Terrabuild.Tests.Core.GraphPipeline.Batch
open FsUnit
open NUnit.Framework
open GraphDef
open GraphPipeline.Batch

[<Test>]
let ``check partition computation``() =
    let buildNode id clusterHash action deps =
        { Node.Id = id
          Node.ProjectId = id
          Node.ProjectName = None
          Node.ProjectDir = $"/src/project{id}"
          Node.Target = "build"
          Node.Dependencies = deps
          Node.Outputs = Set.empty
          Node.ProjectHash = ""
          Node.TargetHash = ""
          Node.ClusterHash = clusterHash
          Node.Operations = []
          Node.Artifacts = Artifacts.Workspace
          Node.Action = action
          Node.Build = Build.Auto
          Node.Batch = Batch.Partition }

    let addNode (node: Node) nodes = nodes |> Map.add node.Id node

    // Bucket hash-A: connected via A1 -> A2 (in-bucket edge)
    let nodeA1 = buildNode "A1" (Some "hash-A") NodeAction.Exec (Set ["A2"; "B1"])
    let nodeA2 = buildNode "A2" (Some "hash-A") NodeAction.Restore Set.empty

    // Bucket hash-B: connected via B1 -> B2 (in-bucket edge)
    let nodeB1 = buildNode "B1" (Some "hash-B") NodeAction.Exec (Set ["B2"])
    let nodeB2 = buildNode "B2" (Some "hash-B") NodeAction.Exec Set.empty

    // Bucket hash-C: connected but inactive (no Build) => no batch
    let nodeC1 = buildNode "C1" (Some "hash-C") NodeAction.Restore (Set ["C2"])
    let nodeC2 = buildNode "C2" (Some "hash-C") NodeAction.Restore Set.empty

    // Not batchable
    let nodeD1 = buildNode "D1" None NodeAction.Exec Set.empty

    let nodes =
        Map.empty
        |> addNode nodeA1 |> addNode nodeA2
        |> addNode nodeB1 |> addNode nodeB2
        |> addNode nodeC1 |> addNode nodeC2
        |> addNode nodeD1

    let graph =
        { Graph.Nodes = nodes
          Graph.RootNodes = Set [ "A1"; "B1"; "D1" ]
          Graph.Batches = Map.empty }

    let batches = computeBatches graph

    let expectedBatchIdA = Hash.sha256strings ("hash-A" :: [ "A1"; "A2" ])
    let expectedBatchIdB = Hash.sha256strings ("hash-B" :: [ "B1"; "B2" ])
    let expectedBatchIdC = Hash.sha256strings ("hash-C" :: [ "C1"; "C2" ])

    let expected =
        [ { BatchId = expectedBatchIdA
            ClusterHash = "hash-A"
            Nodes = [ nodeA1; nodeA2 ] }
          { BatchId = expectedBatchIdB
            ClusterHash = "hash-B"
            Nodes = [ nodeB1; nodeB2 ] }
          { BatchId = expectedBatchIdC
            ClusterHash = "hash-C"
            Nodes = [ nodeC1; nodeC2 ] }]

    // Order is not guaranteed; compare as sets
    batches |> List.map (fun b -> b.BatchId, b.ClusterHash, (b.Nodes |> List.map (fun n -> n.Id) |> Set.ofList))
           |> Set.ofList
    |> should equal (
        expected |> List.map (fun b -> b.BatchId, b.ClusterHash, (b.Nodes |> List.map (fun n -> n.Id) |> Set.ofList))
                 |> Set.ofList
    )



[<Test>]
let ``check partition/all computation``() =
    let buildNode id clusterHash action deps group =
        { Node.Id = id
          Node.ProjectId = id
          Node.ProjectName = None
          Node.ProjectDir = $"/src/project{id}"
          Node.Target = "build"
          Node.Dependencies = deps
          Node.Outputs = Set.empty
          Node.ProjectHash = ""
          Node.TargetHash = ""
          Node.ClusterHash = clusterHash
          Node.Operations = []
          Node.Artifacts = Artifacts.Workspace
          Node.Action = action
          Node.Build = Build.Auto
          Node.Batch = group }

    let addNode (node: Node) nodes = nodes |> Map.add node.Id node

    // Bucket hash-A: connected via A1 -> A2 (in-bucket edge)
    let nodeA1 = buildNode "A1" (Some "hash-A") NodeAction.Exec (Set ["A2"; "B1"]) Batch.Partition
    let nodeA2 = buildNode "A2" (Some "hash-A") NodeAction.Restore Set.empty Batch.Partition

    // Bucket hash-B: connected via B1 -> B2 (in-bucket edge)
    let nodeB1 = buildNode "B1" (Some "hash-B") NodeAction.Exec (Set ["B2"]) Batch.All
    let nodeB2 = buildNode "B2" (Some "hash-B") NodeAction.Exec Set.empty Batch.All
    let nodeC1 = buildNode "C1" (Some "hash-B") NodeAction.Exec (Set ["C2"]) Batch.All
    let nodeC2 = buildNode "C2" (Some "hash-B") NodeAction.Exec Set.empty Batch.All

    // Not batchable
    let nodeD1 = buildNode "D1" None NodeAction.Exec Set.empty Batch.Partition

    let nodes =
        Map.empty
        |> addNode nodeA1 |> addNode nodeA2
        |> addNode nodeB1 |> addNode nodeB2
        |> addNode nodeC1 |> addNode nodeC2
        |> addNode nodeD1

    let graph =
        { Graph.Nodes = nodes
          Graph.RootNodes = Set [ "A1"; "B1"; "D1" ]
          Graph.Batches = Map.empty }

    let batches = computeBatches graph

    let expectedBatchIdA = Hash.sha256strings ("hash-A" :: [ "A1"; "A2" ])
    let expectedBatchIdB = Hash.sha256strings ("hash-B" :: [ "B1"; "B2"; "C1"; "C2" ])

    let expected =
        [ { BatchId = expectedBatchIdA
            ClusterHash = "hash-A"
            Nodes = [ nodeA1; nodeA2 ] }
          { BatchId = expectedBatchIdB
            ClusterHash = "hash-B"
            Nodes = [ nodeB1; nodeB2; nodeC1; nodeC2 ] } ]

    // Order is not guaranteed; compare as sets
    batches |> List.map (fun b -> b.BatchId, b.ClusterHash, (b.Nodes |> List.map (fun n -> n.Id) |> Set.ofList))
           |> Set.ofList
    |> should equal (
        expected |> List.map (fun b -> b.BatchId, b.ClusterHash, (b.Nodes |> List.map (fun n -> n.Id) |> Set.ofList))
                 |> Set.ofList
    )
