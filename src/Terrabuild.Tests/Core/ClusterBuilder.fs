module Terrabuild.Tests.Core.ClusterBuilder
open FsUnit
open NUnit.Framework
open GraphDef

[<Test>]
let ``check cluster computation``() =
    let buildNode id hash deps =
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
          Node.Action = NodeAction.Build }

    let addNode (node: Node) nodes = nodes |> Map.add node.Id node
    let nodeA1 = buildNode "A1" "A" (Set ["B1"])
    let nodeA2 = buildNode "A2" "A" (Set ["B2"])
    let nodeB1 = buildNode "B1" "B" (Set ["C1"; "C2"])
    let nodeB2 = buildNode "B2" "B" (Set ["D1"])
    let nodeC1 = buildNode "C1" "C" (Set.empty)
    let nodeC2 = buildNode "C2" "C" (Set.empty)
    let nodeD1 = buildNode "D1" "D" (Set.empty)
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

    // { Clusters = [
    //     { Id = "hash-A"; Nodes = set ["A1"; "A2"] }
    //     { Id = "hash-B"; Nodes = set ["B1"; "B2"] }
    //     { Id = "hash-C"; Nodes = set ["C1"; "C2"] }
    //     { Id = "hash-D"; Nodes = set ["D1"] } ]
    //   Edges = set [
    //     ("hash-A", "hash-B")
    //     ("hash-B", "hash-C")
    //     ("hash-B", "hash-D") ] }
    let clusters = ClusterBuilder.computeClusters graph
    printfn $"{clusters}"

    let graph = ClusterBuilder.computeClusters graph
    printfn $"{graph}"

