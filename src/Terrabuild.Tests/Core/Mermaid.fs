module Terrabuild.Tests.Core.Mermaid

open FsUnit
open NUnit.Framework
open GraphDef

let private node id phase =
    { Node.Id = id
      Node.ProjectId = id
      Node.ProjectName = Some id
      Node.ProjectDir = id
      Node.Target = "build"
      Node.Phase = phase
      Node.Dependencies = Set.empty
      Node.PhaseDependencies = Set.empty
      Node.Outputs = Set.empty
      Node.ProjectHash = id
      Node.TargetHash = id
      Node.ClusterHash = None
      Node.Operations = []
      Node.Artifacts = ArtifactMode.Workspace
      Node.Build = BuildMode.Auto
      Node.Batch = BatchMode.Single
      Node.Action = RunAction.Exec
      Node.Required = true }

[<Test>]
let ``Mermaid groups phased nodes and renders the participating phase chain`` () =
    let tool = node "tool" (Some "toolchains")
    let app = { node "app" (Some "application") with Dependencies = Set [ tool.Id ] }
    let plain = node "plain" None
    let graph =
        { Graph.Nodes = [ tool; app; plain ] |> List.map (fun item -> item.Id, item) |> Map.ofList
          Graph.RootNodes = Set [ app.Id; plain.Id ]
          Graph.Batches = Map.empty
          Graph.Phases = Map [ "toolchains", Set.empty
                               "empty", Set [ "toolchains" ]
                               "application", Set [ "empty" ] ] }

    let rendered = Mermaid.render None None graph
    let text = rendered |> String.concat "\n"

    text |> should contain "Phase: toolchains"
    text |> should contain "Phase: empty"
    text |> should contain "Phase: application"
    text |> should contain "{\"toolchains\"}"
    text |> should contain "{\"application\"}"
    rendered |> List.filter (fun line -> line.Contains(" -.-> ")) |> List.length |> should equal 2

    let plainIndex = rendered |> List.findIndex (fun line -> line.StartsWith("plain("))
    let firstSubgraphIndex = rendered |> List.findIndex (fun line -> line.StartsWith("subgraph "))
    plainIndex |> should be (lessThan firstSubgraphIndex)

[<Test>]
let ``Mermaid places batch nodes in their phase subgraph`` () =
    let batch = node "batch" (Some "toolchains")
    let graph =
        { Graph.Nodes = Map [ batch.Id, batch ]
          Graph.RootNodes = Set [ batch.Id ]
          Graph.Batches = Map [ batch.Id, Set [ "member-a"; "member-b" ] ]
          Graph.Phases = Map [ "toolchains", Set.empty ] }

    let rendered = Mermaid.render None None graph
    let subgraphIndex = rendered |> List.findIndex (fun line -> line.Contains("Phase: toolchains"))
    let batchIndex = rendered |> List.findIndex (fun line -> line.StartsWith("batch("))
    let endIndex = rendered |> List.findIndex (fun lineIndex -> lineIndex = "end")

    batchIndex |> should be (greaterThan subgraphIndex)
    batchIndex |> should be (lessThan endIndex)
