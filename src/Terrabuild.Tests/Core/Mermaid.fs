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
let ``Mermaid renders phased nodes without phase decoration`` () =
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

    text |> should contain "tool("
    text |> should contain "app("
    text |> should contain "plain("
    text |> should contain "app --> tool"
    text |> should not' (contain "Phase:")
    text |> should not' (contain "subgraph ")
    text |> should not' (contain "_gate")

[<Test>]
let ``Mermaid renders phased batch nodes without phase decoration`` () =
    let batch = node "batch" (Some "toolchains")
    let graph =
        { Graph.Nodes = Map [ batch.Id, batch ]
          Graph.RootNodes = Set [ batch.Id ]
          Graph.Batches = Map [ batch.Id, Set [ "member-a"; "member-b" ] ]
          Graph.Phases = Map [ "toolchains", Set.empty ] }

    let rendered = Mermaid.render None None graph
    let text = rendered |> String.concat "\n"

    text |> should contain "batch("
    text |> should not' (contain "Phase:")
    text |> should not' (contain "subgraph ")
