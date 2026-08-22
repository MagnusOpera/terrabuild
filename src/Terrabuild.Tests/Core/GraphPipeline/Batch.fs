module Terrabuild.Tests.Core.GraphPipeline.Batch
open System
open FsUnit
open NUnit.Framework
open GraphDef
open GraphPipeline.Batch
open Terrabuild.Expression

let buildNode id clusterHash action deps group req =
    { Node.Id = id
      Node.ProjectId = id
      Node.ProjectName = None
      Node.ProjectDir = $"/src/project{id}"
      Node.Target = "build"
      Node.Phase = None
      Node.Dependencies = deps
      Node.PhaseDependencies = Set.empty
      Node.Outputs = Set.empty
      Node.ProjectHash = ""
      Node.TargetHash = ""
      Node.ClusterHash = clusterHash
      Node.Operations = []
      Node.EvaluationInputs = []
      Node.EnvironmentSensitive = None
      Node.Artifacts = ArtifactMode.Workspace
      Node.Action = action
      Node.Build = BuildMode.Auto
      Node.Batch = group
      Node.Required = req }

let addNode (node: Node) nodes = nodes |> Map.add node.Id node

let buildOperation envs =
    { ContaineredShellOperation.Image = None
      ContaineredShellOperation.Platform = None
      ContaineredShellOperation.Cpus = None
      ContaineredShellOperation.Variables = Set.empty
      ContaineredShellOperation.Envs = envs
      ContaineredShellOperation.MetaCommand = "@test build"
      ContaineredShellOperation.Command = "test"
      ContaineredShellOperation.Arguments = ""
      ContaineredShellOperation.ErrorLevel = 0
      ContaineredShellOperation.Stdout = None }

[<Test>]
let ``batch environments merge disjoint and identical values`` () =
    let environments =
        mergeBatchEnvironments
            "build"
            "@pnpm build"
            [ "web:build", Map [ "SHARED", "same"; "WEB", "web" ]
              "admin:build", Map [ "ADMIN", "admin"; "SHARED", "same" ] ]

    environments
    |> should equal (Map [ "ADMIN", "admin"; "SHARED", "same"; "WEB", "web" ])

[<Test>]
let ``batch environments reject conflicting values without exposing them`` () =
    let build () =
        mergeBatchEnvironments
            "build"
            "@pnpm build"
            [ "web:build", Map [ "APP_VERSION", "secret-web-value" ]
              "admin:build", Map [ "APP_VERSION", "secret-admin-value" ] ]
        |> ignore

    let error =
        Assert.Throws<Errors.TerrabuildException>(Action build)
        |> Option.ofObj
        |> Option.defaultWith (fun () -> failwith "Expected TerrabuildException")

    error.Message
    |> should equal "Cannot batch target 'build' step '@pnpm build': environment variable 'APP_VERSION' has conflicting values for 'admin:build', 'web:build'. Configure batch = ~never for targets that require project-specific values."
    error.Area |> should equal Errors.ErrorArea.InvalidArg
    error.Message |> should not' (contain "secret-web-value")
    error.Message |> should not' (contain "secret-admin-value")

[<Test>]
let ``batch variables include every member variable`` () =
    mergeBatchVariables
        [ Set [ "SHARED"; "WEB" ]
          Set [ "ADMIN"; "SHARED" ] ]
    |> should equal (Set [ "ADMIN"; "SHARED"; "WEB" ])

[<Test>]
let ``batch action contexts merge disjoint and identical values`` () =
    mergeBatchContexts
        "build"
        "@pnpm build"
        [ "web:build", Value.Map (Map [ "shared", Value.String "same"; "web", Value.Bool true ])
          "admin:build", Value.Map (Map [ "admin", Value.Bool true; "shared", Value.String "same" ]) ]
    |> should equal
        (Value.Map (
            Map [ "admin", Value.Bool true
                  "shared", Value.String "same"
                  "web", Value.Bool true ]
        ))

[<Test>]
let ``batch action contexts reject conflicting values without exposing them`` () =
    let build () =
        mergeBatchContexts
            "build"
            "@pnpm build"
            [ "web:build", Value.Map (Map [ "mode", Value.String "secret-web-value" ])
              "admin:build", Value.Map (Map [ "mode", Value.String "secret-admin-value" ]) ]
        |> ignore

    let error =
        Assert.Throws<Errors.TerrabuildException>(Action build)
        |> Option.ofObj
        |> Option.defaultWith (fun () -> failwith "Expected TerrabuildException")

    error.Message
    |> should equal "Cannot batch target 'build' step '@pnpm build': action argument 'mode' has conflicting values for 'admin:build', 'web:build'. Configure batch = ~never for targets that require project-specific values."
    error.Area |> should equal Errors.ErrorArea.InvalidArg
    error.Message |> should not' (contain "secret-web-value")
    error.Message |> should not' (contain "secret-admin-value")

[<Test>]
let ``batch target hash includes every member target hash and merged operations`` () =
    let nodeA =
        { buildNode "A" (Some "cluster") RunAction.Exec Set.empty BatchMode.Single true with
            TargetHash = "target-a" }
    let nodeB =
        { buildNode "B" (Some "cluster") RunAction.Exec Set.empty BatchMode.Single true with
            TargetHash = "target-b" }
    let batch =
        { BatchId = "batch"
          ClusterHash = "cluster"
          Phase = None
          Nodes = [ nodeA; nodeB ] }

    let original = computeBatchTargetHash batch [ buildOperation (Map [ "A", "one" ]) ]
    let changedMember =
        computeBatchTargetHash
            { batch with Nodes = [ nodeA; { nodeB with TargetHash = "target-b-changed" } ] }
            [ buildOperation (Map [ "A", "one" ]) ]
    let changedEnvironment =
        computeBatchTargetHash batch [ buildOperation (Map [ "A", "two" ]) ]
    let changedVariables =
        computeBatchTargetHash
            batch
            [ { buildOperation (Map [ "A", "one" ]) with
                  Variables = Set [ "FORWARDED" ] } ]

    changedMember |> should not' (equal original)
    changedEnvironment |> should not' (equal original)
    changedVariables |> should not' (equal original)

[<Test>]
let ``check partition computation``() =
    // Bucket hash-A: connected via A1 -> A2 (in-bucket edge)
    let nodeA1 = buildNode "A1" (Some "hash-A") RunAction.Exec (Set ["A2"; "B1"]) BatchMode.Partition true
    let nodeA2 = buildNode "A2" (Some "hash-A") RunAction.Restore Set.empty BatchMode.Partition true

    // Bucket hash-B: connected via B1 -> B2 (in-bucket edge)
    let nodeB1 = buildNode "B1" (Some "hash-B") RunAction.Exec (Set ["B2"]) BatchMode.Partition true
    let nodeB2 = buildNode "B2" (Some "hash-B") RunAction.Exec Set.empty BatchMode.Partition true

    // Bucket hash-C: connected but inactive (no Build) => no batch
    let nodeC1 = buildNode "C1" (Some "hash-C") RunAction.Restore (Set ["C2"]) BatchMode.Partition true
    let nodeC2 = buildNode "C2" (Some "hash-C") RunAction.Restore Set.empty BatchMode.Partition true

    // Not batchable
    let nodeD1 = buildNode "D1" None RunAction.Exec Set.empty BatchMode.Partition true

    let nodes =
        Map.empty
        |> addNode nodeA1 |> addNode nodeA2
        |> addNode nodeB1 |> addNode nodeB2
        |> addNode nodeC1 |> addNode nodeC2
        |> addNode nodeD1

    let graph =
        { Graph.Nodes = nodes
          Graph.RootNodes = Set [ "A1"; "B1"; "D1" ]
          Graph.Batches = Map.empty
          Graph.Phases = Map.empty }

    let batches = computeBatches graph

    let expectedBatchIdA = Hash.sha256strings ("hash-A" :: [ "A1"; "A2" ])
    let expectedBatchIdB = Hash.sha256strings ("hash-B" :: [ "B1"; "B2" ])

    let expected =
        [ { BatchId = expectedBatchIdA
            ClusterHash = "hash-A"
            Phase = None
            Nodes = [ nodeA1; nodeA2 ] }
          { BatchId = expectedBatchIdB
            ClusterHash = "hash-B"
            Phase = None
            Nodes = [ nodeB1; nodeB2 ] }]

    // Order is not guaranteed; compare as sets
    batches |> List.map (fun b -> b.BatchId, b.ClusterHash, (b.Nodes |> List.map (fun n -> n.Id) |> Set.ofList))
           |> Set.ofList
    |> should equal (
        expected |> List.map (fun b -> b.BatchId, b.ClusterHash, (b.Nodes |> List.map (fun n -> n.Id) |> Set.ofList))
                 |> Set.ofList
    )



[<Test>]
let ``check partition/all computation``() =
    // Bucket hash-A: connected via A1 -> A2 (in-bucket edge)
    let nodeA1 = buildNode "A1" (Some "hash-A") RunAction.Restore (Set ["A2"; "B1"]) BatchMode.Partition true
    let nodeA2 = buildNode "A2" (Some "hash-A") RunAction.Restore Set.empty BatchMode.Partition true

    // Bucket hash-B: connected via B1 -> B2 (in-bucket edge)
    let nodeB1 = buildNode "B1" (Some "hash-B") RunAction.Exec (Set ["B2"]) BatchMode.Single true
    let nodeB2 = buildNode "B2" (Some "hash-B") RunAction.Exec Set.empty BatchMode.Single true
    let nodeC1 = buildNode "C1" (Some "hash-B") RunAction.Exec (Set ["C2"]) BatchMode.Single true
    let nodeC2 = buildNode "C2" (Some "hash-B") RunAction.Exec Set.empty BatchMode.Single true

    // Not batchable
    let nodeD1 = buildNode "D1" None RunAction.Exec Set.empty BatchMode.Partition true

    let nodes =
        Map.empty
        |> addNode nodeA1 |> addNode nodeA2
        |> addNode nodeB1 |> addNode nodeB2
        |> addNode nodeC1 |> addNode nodeC2
        |> addNode nodeD1

    let graph =
        { Graph.Nodes = nodes
          Graph.RootNodes = Set [ "A1"; "B1"; "D1" ]
          Graph.Batches = Map.empty
          Graph.Phases = Map.empty }

    let batches = computeBatches graph

    let expectedBatchIdB = Hash.sha256strings ("hash-B" :: [ "B1"; "B2"; "C1"; "C2" ])

    let expected =
        [ { BatchId = expectedBatchIdB
            ClusterHash = "hash-B"
            Phase = None
            Nodes = [ nodeB1; nodeB2; nodeC1; nodeC2 ] } ]

    // Order is not guaranteed; compare as sets
    batches |> List.map (fun b -> b.BatchId, b.ClusterHash, (b.Nodes |> List.map (fun n -> n.Id) |> Set.ofList))
           |> Set.ofList
    |> should equal (
        expected |> List.map (fun b -> b.BatchId, b.ClusterHash, (b.Nodes |> List.map (fun n -> n.Id) |> Set.ofList))
                 |> Set.ofList
    )

[<Test>]
let ``batch computation skips candidates that would create an external dependency cycle``() =
    let libA = buildNode "libA" (Some "hash-build") RunAction.Exec Set.empty BatchMode.Partition true
    let libB = buildNode "libB" (Some "hash-build") RunAction.Exec Set.empty BatchMode.Partition true
    let app = buildNode "app" (Some "hash-build") RunAction.Exec (Set [ "libA"; "libB"; "tool" ]) BatchMode.Partition true
    let tool = buildNode "tool" (Some "hash-tool") RunAction.Exec (Set [ "libA"; "libB" ]) BatchMode.Partition true

    let nodes =
        Map.empty
        |> addNode libA
        |> addNode libB
        |> addNode app
        |> addNode tool

    let graph =
        { Graph.Nodes = nodes
          Graph.RootNodes = Set [ "app" ]
          Graph.Batches = Map.empty
          Graph.Phases = Map.empty }

    let batches = computeBatches graph

    batches
    |> List.exists (fun batch -> batch.Nodes |> List.exists (fun node -> node.Id = "app"))
    |> should equal false

[<Test>]
let ``batch computation drops every batch in a contracted execution cycle``() =
    let nodeA1 = buildNode "A1" (Some "hash-A") RunAction.Exec (Set [ "B1" ]) BatchMode.Single true
    let nodeA2 = buildNode "A2" (Some "hash-A") RunAction.Exec Set.empty BatchMode.Single true
    let nodeB1 = buildNode "B1" (Some "hash-B") RunAction.Exec Set.empty BatchMode.Single true
    let nodeB2 = buildNode "B2" (Some "hash-B") RunAction.Exec (Set [ "A2" ]) BatchMode.Single true
    let nodeC1 = buildNode "C1" (Some "hash-C") RunAction.Exec Set.empty BatchMode.Single true
    let nodeC2 = buildNode "C2" (Some "hash-C") RunAction.Exec Set.empty BatchMode.Single true

    let nodes =
        [ nodeA1; nodeA2; nodeB1; nodeB2; nodeC1; nodeC2 ]
        |> List.map (fun node -> node.Id, node)
        |> Map.ofList

    let graph =
        { Graph.Nodes = nodes
          Graph.RootNodes = Set [ "A1"; "B2"; "C1"; "C2" ]
          Graph.Batches = Map.empty
          Graph.Phases = Map.empty }

    let batches = computeBatches graph

    batches
    |> List.map (fun batch -> batch.ClusterHash, batch.Nodes |> List.map _.Id |> Set.ofList)
    |> should equal [ ("hash-C", Set [ "C1"; "C2" ]) ]

[<Test>]
let ``batch computation never mixes phases or phased and unphased nodes`` () =
    let toolA = { buildNode "toolA" (Some "cluster") RunAction.Exec Set.empty BatchMode.Single true with Phase = Some "toolchains" }
    let toolB = { buildNode "toolB" (Some "cluster") RunAction.Exec Set.empty BatchMode.Single true with Phase = Some "toolchains" }
    let appA = { buildNode "appA" (Some "cluster") RunAction.Exec Set.empty BatchMode.Single true with Phase = Some "application" }
    let appB = { buildNode "appB" (Some "cluster") RunAction.Exec Set.empty BatchMode.Single true with Phase = Some "application" }
    let plainA = buildNode "plainA" (Some "cluster") RunAction.Exec Set.empty BatchMode.Single true
    let plainB = buildNode "plainB" (Some "cluster") RunAction.Exec Set.empty BatchMode.Single true
    let nodes = [ toolA; toolB; appA; appB; plainA; plainB ]
    let graph =
        { Graph.Nodes = nodes |> List.map (fun node -> node.Id, node) |> Map.ofList
          Graph.RootNodes = nodes |> List.map _.Id |> Set.ofList
          Graph.Batches = Map.empty
          Graph.Phases = Map [ "application", Set [ "toolchains" ]; "toolchains", Set.empty ] }

    let batches = computeBatches graph

    batches |> List.length |> should equal 3
    batches
    |> List.map (fun batch -> batch.Phase, batch.Nodes |> List.map _.Id |> Set.ofList)
    |> Set.ofList
    |> should equal (Set [ (Some "toolchains", Set [ "toolA"; "toolB" ]);
                          (Some "application", Set [ "appA"; "appB" ]);
                          (None, Set [ "plainA"; "plainB" ]) ])
