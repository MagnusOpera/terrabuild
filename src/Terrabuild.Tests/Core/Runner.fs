module Terrabuild.Tests.Core.Runner
open FsUnit
open NUnit.Framework

let private buildNode id projectDir target =
    { GraphDef.Node.Id = id
      GraphDef.Node.ProjectId = id
      GraphDef.Node.ProjectName = None
      GraphDef.Node.ProjectDir = projectDir
      GraphDef.Node.Target = target
      GraphDef.Node.Dependencies = Set.empty
      GraphDef.Node.Outputs = Set.empty
      GraphDef.Node.ProjectHash = $"project-{id}"
      GraphDef.Node.TargetHash = $"target-{id}"
      GraphDef.Node.ClusterHash = Some "cluster"
      GraphDef.Node.Operations = []
      GraphDef.Node.Artifacts = GraphDef.ArtifactMode.Workspace
      GraphDef.Node.Build = GraphDef.BuildMode.Auto
      GraphDef.Node.Batch = GraphDef.BatchMode.Single
      GraphDef.Node.Action = GraphDef.RunAction.Ignore
      GraphDef.Node.Required = true }

[<Test>]
let ``buildBatchSchedule flattens member labels in GitHub mode`` () =
    let memberA = buildNode "node-a" "apps/Api" "install"
    let memberB = buildNode "node-b" "libs/MagnusOpera.DbModels.Insights" "install"
    let batchNode = buildNode "batch-install" "." "install"
    let graph =
        { GraphDef.Graph.Nodes =
            [ memberA.Id, memberA
              memberB.Id, memberB
              batchNode.Id, batchNode ] |> Map.ofList
          GraphDef.Graph.RootNodes = Set [ memberA.Id; memberB.Id ]
          GraphDef.Graph.Batches = Map [ batchNode.Id, Set [ memberA.Id; memberB.Id ] ] }

    let schedule = Runner.buildBatchSchedule true graph batchNode (Some (Set [ memberA.Id; memberB.Id ]))

    schedule |> should equal [ (memberA.Id, "install apps/Api"); (memberB.Id, "install libs/MagnusOpera.DbModels.Insights") ]

[<Test>]
let ``buildBatchSchedule keeps hierarchical labels outside GitHub mode`` () =
    let memberA = buildNode "node-a" "apps/Api" "install"
    let memberB = buildNode "node-b" "libs/MagnusOpera.DbModels.Insights" "install"
    let batchNode = buildNode "batch-install" "." "install"
    let graph =
        { GraphDef.Graph.Nodes =
            [ memberA.Id, memberA
              memberB.Id, memberB
              batchNode.Id, batchNode ] |> Map.ofList
          GraphDef.Graph.RootNodes = Set [ memberA.Id; memberB.Id ]
          GraphDef.Graph.Batches = Map [ batchNode.Id, Set [ memberA.Id; memberB.Id ] ] }

    let schedule = Runner.buildBatchSchedule false graph batchNode (Some (Set [ memberA.Id; memberB.Id ]))

    schedule[0] |> should equal (batchNode.Id, "install")
    schedule[1] |> should equal (memberA.Id, $" {Ansi.Styles.dimwhite}⦙{Ansi.Styles.reset} apps/Api")
    schedule[2] |> should equal (memberB.Id, $" {Ansi.Styles.dimwhite}⦙{Ansi.Styles.reset} libs/MagnusOpera.DbModels.Insights")
