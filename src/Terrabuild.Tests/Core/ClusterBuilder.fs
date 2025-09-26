module Terrabuild.Tests.Core.ClusterBuilder
open FsUnit
open NUnit.Framework
open GraphDef
open ClusterBuilder

[<Test>]
let ``check cluster computation``() =
    let buildNode id hash action deps =
        { Node.Id = id
          Node.ProjectId = None
          Node.ProjectDir = $"/src/project{id}"
          Node.Target = "build"
          Node.Dependencies = deps
          Node.Outputs = Set.empty
          Node.ProjectHash = ""
          Node.TargetHash = ""
          Node.ClusterHash = $"hash-{hash}"
          Node.Operations = []
          Node.Cache = Terrabuild.Extensibility.Cacheability.Local
          Node.IsLeaf = false
          Node.Action = action }

    let addNode (node: Node) nodes = nodes |> Map.add node.Id node
    let nodeA1 = buildNode "A1" "A" NodeAction.Build (Set ["B1"])
    let nodeA2 = buildNode "A2" "A" NodeAction.Build (Set ["B2"])
    let nodeB1 = buildNode "B1" "B" NodeAction.Build (Set ["C1"; "C2"])
    let nodeB2 = buildNode "B2" "B" NodeAction.Build (Set ["D1"])
    let nodeC1 = buildNode "C1" "C-Build" NodeAction.Build (Set.empty)
    let nodeC2 = buildNode "C2" "C-Restore" NodeAction.Restore (Set.empty)
    let nodeD1 = buildNode "D1" "D" NodeAction.Build (Set.empty)
    let nodes =
        Map.empty
        |> addNode nodeA1 |> addNode nodeA2
        |> addNode nodeB1 |> addNode nodeB2
        |> addNode nodeC1 |> addNode nodeC2
        |> addNode nodeD1

    printfn $"{nodes}"

    let graph =
        { Graph.Nodes = nodes
          Graph.RootNodes = Set [ nodeA1.Id; nodeA2.Id ]
          Graph.Clusters = Map.empty }

    let expectedClusters =
        [ { Id = "hash-A"; Nodes = set ["A1"; "A2"] }
          { Id = "hash-B"; Nodes = set ["B1"; "B2"] } ]
    let clusters = ClusterBuilder.computeClusters graph
    printfn $"{clusters}"
    clusters |> should equal expectedClusters
