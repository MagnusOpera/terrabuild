module Terrabuild.Tests.Core.Program
open System
open System.IO
open System.Text.Json
open Argu
open CLI
open FsUnit
open FsUnitTyped
open NUnit.Framework
open Contracts

let private buildNodeInfo project target status =
    { Runner.NodeInfo.Provenance = None
      Runner.NodeInfo.Request = Runner.TaskRequest.Exec
      Runner.NodeInfo.Status = status
      Runner.NodeInfo.Project = project
      Runner.NodeInfo.Target = target
      Runner.NodeInfo.ProjectHash = $"project-{project}"
      Runner.NodeInfo.TargetHash = $"target-{target}" }

let private buildSummary isSuccess nodes =
    { Runner.Summary.Commit = "commit"
      Runner.Summary.BranchOrTag = "main"
      Runner.Summary.StartedAt = DateTime.Parse("2026-04-11T16:42:46.595161Z").ToUniversalTime()
      Runner.Summary.EndedAt = DateTime.Parse("2026-04-11T16:43:28.917020Z").ToUniversalTime()
      Runner.Summary.IsSuccess = isSuccess
      Runner.Summary.Targets = Set [ "build"; "test" ]
      Runner.Summary.Nodes = nodes |> Map.ofList }

let private buildGraph (nodes: GraphDef.Node list) =
    { GraphDef.Graph.Nodes = nodes |> List.map (fun node -> node.Id, node) |> Map.ofList
      GraphDef.Graph.RootNodes = nodes |> List.map (fun node -> node.Id) |> Set.ofList
      GraphDef.Graph.Batches = Map.empty
      GraphDef.Graph.Phases = Map.empty }

let private buildGraphNode id projectName projectDir target =
    { GraphDef.Node.Id = id
      GraphDef.Node.ProjectId = id
      GraphDef.Node.ProjectName = projectName
      GraphDef.Node.ProjectDir = projectDir
      GraphDef.Node.Target = target
      GraphDef.Node.Phase = None
      GraphDef.Node.Locks = Set.empty
      GraphDef.Node.Dependencies = Set.empty
      GraphDef.Node.PhaseDependencies = Set.empty
      GraphDef.Node.Outputs = Set.empty
      GraphDef.Node.ProjectHash = $"project-{id}"
      GraphDef.Node.TargetHash = $"target-{id}"
      GraphDef.Node.ClusterHash = None
      GraphDef.Node.CanReuseCache = true
      GraphDef.Node.Provenance = None
      GraphDef.Node.Operations = []
      GraphDef.Node.EvaluationInputs = []
      GraphDef.Node.EnvironmentSensitive = None
      GraphDef.Node.Artifacts = GraphDef.ArtifactMode.Workspace
      GraphDef.Node.Build = GraphDef.BuildMode.Auto
      GraphDef.Node.Batch = GraphDef.BatchMode.Single
      GraphDef.Node.Action = GraphDef.RunAction.Ignore
      GraphDef.Node.Required = true }

let private withAction action node =
    { node with GraphDef.Node.Action = action }

let private withTempDir action =
    let root = Path.Combine(Path.GetTempPath(), $"terrabuild-program-tests-{Guid.NewGuid():N}")
    Directory.CreateDirectory(root) |> ignore
    try
        action root
    finally
        if Directory.Exists(root) then
            Directory.Delete(root, true)

let private hasJsonProperty (json: JsonElement) propertyName =
    json.EnumerateObject() |> Seq.exists (fun property -> property.Name = propertyName)

type private RecordingImpactApiClient() =
    let mutable lastLookup: (string * string * string) option = None

    member _.LastLookup = lastLookup

    interface IApiClient with
        member _.StartBuild() = ()
        member _.UploadBuildGraph _graphHash _environment _nodes = ()
        member _.CompleteBuild _success = ()
        member _.AddArtifact _project _projectName _target _projectHash _targetHash _files _success _startedAt _endedAt _provenance = ()
        member _.UseArtifact _projectHash _hash _productionId = ()
        member _.LookupArtifacts _entries = failwith "Unused in this test"
        member _.GetUploadLocation _path = Uri("https://example.invalid/artifact"), None
        member _.GetCommitGraph repository commit environment =
            lastLookup <- Some (repository, commit, environment)
            { CommitGraph.Repository = repository
              CommitGraph.Commit = commit
              CommitGraph.GraphHash = "graph"
              CommitGraph.Nodes = [] }

[<Test>]
let ``preparation failures finalize the latest diagnostic snapshot`` () =
    let mutable diagnostic = None

    Assert.That(
        Action(fun () ->
            global.Program.runWithFailureDiagnostic
                (fun status completeness error ->
                    diagnostic <- Some (status, completeness, error))
                (fun () -> raise (InvalidOperationException("analysis failed")))
            |> ignore),
        Throws.TypeOf<InvalidOperationException>())

    diagnostic
    |> shouldEqual (Some ("failure", "partial", Some "analysis failed"))

[<Test>]
let ``atomic text replacement leaves a complete destination and no staging file`` () =
    withTempDir (fun root ->
        let destination = Path.Combine(root, "terrabuild-debug.json")
        File.WriteAllText(destination, "old")

        IO.writeTextFileAtomic destination "new diagnostic"

        File.ReadAllText(destination) |> should equal "new diagnostic"
        Directory.EnumerateFiles(root, ".terrabuild-debug.json.*.tmp")
        |> Seq.isEmpty
        |> should equal true)

[<Test>]
let ``diagnostic write failures do not escape`` () =
    let invalidContext = Unchecked.defaultof<Diagnostics.Context>

    Assert.DoesNotThrow(Action(fun () -> Diagnostics.tryWrite "/missing-parent/terrabuild-debug.json" invalidContext))

[<Test>]
let ``explanation sensitivity summary reports no selected violations`` () =
    Diagnostics.renderEnvironmentSensitivitySummary []
    |> should equal "Environment sensitivity: no selected targets need attention."

[<Test>]
let ``explanation sensitivity summary reports one selected violation`` () =
    Diagnostics.renderEnvironmentSensitivitySummary
        [ "workspace/path#toolchains/pnpm:dist", [ "terrabuild.ci" ] ]
    |> should equal
        "Environment sensitivity: 1 selected target consumes environment-sensitive inputs without opting in.\n  - workspace/path#toolchains/pnpm:dist: terrabuild.ci\n  Remove the environment-sensitive inputs or set environment_sensitive = true for intentionally environment-specific artifacts."

[<Test>]
let ``explanation sensitivity summary reports multiple selected violations`` () =
    Diagnostics.renderEnvironmentSensitivitySummary
        [ "workspace/path#toolchains/cloudflare:dist", [ "terrabuild.ci" ]
          "workspace/path#toolchains/pnpm:dist", [ "terrabuild.ci"; "terrabuild.environment" ] ]
    |> should equal
        "Environment sensitivity: 2 selected targets consume environment-sensitive inputs without opting in.\n  - workspace/path#toolchains/cloudflare:dist: terrabuild.ci\n  - workspace/path#toolchains/pnpm:dist: terrabuild.ci, terrabuild.environment\n  Remove the environment-sensitive inputs or set environment_sensitive = true for intentionally environment-specific artifacts."

[<Test>]
let ``getImpactBaseGraph uses resolved environment key`` () =
    let api = RecordingImpactApiClient()
    let options: ConfigOptions.Options =
        { Workspace = "."
          HomeDir = "."
          TmpDir = "."
          SharedDir = "."
          DryRun = false
          Debug = false
          MaxConcurrency = 1
          Force = false
          Retry = false
          LocalOnly = false
          StartedAt = DateTime.UtcNow
          Targets = Set [ "build" ]
          Configuration = None
          Environment = Some "dev-staging"
          LogTypes = []
          Note = None
          GroupId = None
          Label = None
          Types = None
          Labels = None
          Projects = None
          Variables = Map.empty
          Engine = ConfigOptions.Engine.Host
          BranchOrTag = "main"
          Repository = "acme/repo"
          HeadCommit =
            { Commit.Sha = "head"
              Commit.Author = "dev"
              Commit.Email = "dev@example.com"
              Commit.Message = "head"
              Commit.Timestamp = DateTime.UtcNow }
          CommitLog = []
          Run = None }

    global.Program.getImpactBaseGraph (api :> IApiClient) options "base-sha" |> ignore

    api.LastLookup |> shouldEqual (Some ("acme/repo", "base-sha", "dev-staging"))

[<Test>]
let ``CLI parses out argument for run`` () =
    let parser = ArgumentParser.Create<CLI.TerrabuildArgs>(programName = "terrabuild")
    let result = parser.ParseCommandLine([| "run"; "build"; "--out"; "out.json" |], raiseOnUsage = true)
    let runArgs = result.GetResult(TerrabuildArgs.Run)

    runArgs.GetResult(RunArgs.Out) |> should equal "out.json"

[<Test>]
let ``CLI accepts dry run and rejects removed what if option`` () =
    let parser = ArgumentParser.Create<CLI.TerrabuildArgs>(programName = "terrabuild")
    let result = parser.ParseCommandLine([| "run"; "build"; "--dry-run" |], raiseOnUsage = true)

    result.GetResult(TerrabuildArgs.Run).Contains(RunArgs.Dry_Run)
    |> should equal true

    let removedResult =
        parser.ParseCommandLine([| "run"; "build"; "--what-if" |], raiseOnUsage = true)
    let removedTargets = removedResult.GetResult(TerrabuildArgs.Run).GetResult(RunArgs.Target)

    Assert.Throws<Errors.TerrabuildException>(Action(fun () ->
        global.Program.normalizeTargetNames removedTargets |> Seq.toList |> ignore))
    |> shouldNotEqual null

[<Test>]
let ``CLI parses optional build group for run`` () =
    let parser = ArgumentParser.Create<CLI.TerrabuildArgs>(programName = "terrabuild")
    let grouped = parser.ParseCommandLine([| "run"; "build"; "--group"; "deployment-123" |], raiseOnUsage = true)
    let ungrouped = parser.ParseCommandLine([| "run"; "build" |], raiseOnUsage = true)

    grouped.GetResult(TerrabuildArgs.Run).TryGetResult(RunArgs.Group)
    |> shouldEqual (Some "deployment-123")

    ungrouped.GetResult(TerrabuildArgs.Run).TryGetResult(RunArgs.Group)
    |> shouldEqual None

[<Test>]
let ``CLI parses explain target selection`` () =
    let parser = ArgumentParser.Create<CLI.TerrabuildArgs>(programName = "terrabuild")
    let result =
        parser.ParseCommandLine(
            [| "explain"; "build"; "--environment"; "staging"; "--project"; "app"; "--force" |],
            raiseOnUsage = true)
    let explainArgs = result.GetResult(TerrabuildArgs.Explain)

    explainArgs.GetResult(ExplainArgs.Target) |> should equal [ "build" ]
    explainArgs.GetResult(ExplainArgs.Environment) |> should equal "staging"
    explainArgs.GetResult(ExplainArgs.Project) |> should equal [ "app" ]
    explainArgs.Contains(ExplainArgs.Force) |> should equal true

[<Test>]
let ``build context serializes optional group identifier`` () =
    let context groupId : Api.Build.BuildContextInput =
        { Configuration = None
          Environment = None
          Note = None
          GroupId = groupId
          Tag = None
          Targets = [ "build" ]
          Force = false
          Retry = false }

    use grouped = Json.Serialize(context (Some "deployment-123")) |> JsonDocument.Parse
    use ungrouped = Json.Serialize(context None) |> JsonDocument.Parse

    grouped.RootElement.GetProperty("groupId").GetString()
    |> shouldEqual "deployment-123"

    hasJsonProperty ungrouped.RootElement "groupId"
    |> should equal false

[<Test>]
let ``CLI parses impact base and out arguments`` () =
    let parser = ArgumentParser.Create<CLI.TerrabuildArgs>(programName = "terrabuild")
    let result = parser.ParseCommandLine([| "impact"; "build"; "--base"; "abc123"; "--out"; "impact.json" |], raiseOnUsage = true)
    let impactArgs = result.GetResult(TerrabuildArgs.Impact)

    impactArgs.GetResult(ImpactArgs.Base) |> should equal "abc123"
    impactArgs.GetResult(ImpactArgs.Out) |> should equal "impact.json"

[<Test>]
let ``buildRunResult produces minimal jq friendly structure`` () =
    let graph =
        buildGraph
            [ buildGraphNode "node-a" (Some "root") "." "build" |> withAction GraphDef.RunAction.Exec
              buildGraphNode "node-b" (Some "Terrabuild.Common") "src/Terrabuild.Common" "build" |> withAction GraphDef.RunAction.Restore
              buildGraphNode "node-c" (Some "Terrabuild.Common.Tests") "src/Terrabuild.Common.Tests" "test" |> withAction GraphDef.RunAction.Summary ]

    let summary =
        buildSummary true
            [ "node-a", buildNodeInfo "." "build" (Runner.TaskStatus.Success DateTime.UtcNow)
              "node-b", buildNodeInfo "src/Terrabuild.Common" "build" (Runner.TaskStatus.Success DateTime.UtcNow)
              "node-c", buildNodeInfo "src/Terrabuild.Common.Tests" "test" (Runner.TaskStatus.Success DateTime.UtcNow) ]

    let runResult = global.Program.buildRunResult graph summary

    runResult.Status |> should equal "success"
    runResult.Targets |> should equal [ "build"; "test" ]
    runResult.Results["root:build"] |> should equal "success"
    runResult.Results["terrabuild.common:build"] |> should equal "success"
    runResult.Results["terrabuild.common.tests:test"] |> should equal "success"

[<Test>]
let ``buildRunResult collapses duplicate project target keys with failure dominance`` () =
    let graph =
        buildGraph
            [ buildGraphNode "node-a" (Some "App") "src/App" "build" |> withAction GraphDef.RunAction.Exec
              buildGraphNode "node-b" (Some "App") "src/App" "build" |> withAction GraphDef.RunAction.Exec
              buildGraphNode "node-c" (Some "Lib") "src/Lib" "build" |> withAction GraphDef.RunAction.Restore ]

    let summary =
        buildSummary false
            [ "node-a", buildNodeInfo "src/App" "build" (Runner.TaskStatus.Success DateTime.UtcNow)
              "node-b", buildNodeInfo "src/App" "build" (Runner.TaskStatus.Failure (DateTime.UtcNow, "boom"))
              "node-c", buildNodeInfo "src/Lib" "build" (Runner.TaskStatus.Success DateTime.UtcNow) ]

    let runResult = global.Program.buildRunResult graph summary

    runResult.Status |> should equal "failure"
    runResult.Results.Count |> should equal 2
    runResult.Results["app:build"] |> should equal "failure"
    runResult.Results["lib:build"] |> should equal "success"

[<Test>]
let ``buildRunResult marks named graph nodes without runtime results as ignored and skips unnamed nodes`` () =
    let graph =
        buildGraph
            [ buildGraphNode "node-a" (Some "Root") "." "build" |> withAction GraphDef.RunAction.Exec
              buildGraphNode "node-b" (Some "App") "src/App" "test" |> withAction GraphDef.RunAction.Ignore
              buildGraphNode "node-c" (Some "Lib") "src/Lib" "build" |> withAction GraphDef.RunAction.Restore
              buildGraphNode "node-d" None "src/Unnamed" "build" |> withAction GraphDef.RunAction.Exec ]

    let summary =
        buildSummary true
            [ "node-a", buildNodeInfo "." "build" (Runner.TaskStatus.Success DateTime.UtcNow)
              "node-c", buildNodeInfo "src/Lib" "build" (Runner.TaskStatus.Success DateTime.UtcNow) ]

    let runResult = global.Program.buildRunResult graph summary

    runResult.Status |> should equal "success"
    runResult.Results.Count |> should equal 3
    runResult.Results["root:build"] |> should equal "success"
    runResult.Results["app:test"] |> should equal "ignored"
    runResult.Results["lib:build"] |> should equal "success"

[<Test>]
let ``buildRunResult uses failure then success then ignored precedence for duplicate keys`` () =
    let graph =
        buildGraph
            [ buildGraphNode "node-a" (Some "App") "src/App" "build" |> withAction GraphDef.RunAction.Ignore
              buildGraphNode "node-b" (Some "App") "src/App" "build" |> withAction GraphDef.RunAction.Restore
              buildGraphNode "node-c" (Some "App") "src/App" "build" |> withAction GraphDef.RunAction.Exec ]

    let summary =
        buildSummary true
            [ "node-b", buildNodeInfo "src/App" "build" (Runner.TaskStatus.Success DateTime.UtcNow) ]

    let runResult = global.Program.buildRunResult graph summary

    runResult.Results.Count |> should equal 1
    runResult.Results["app:build"] |> should equal "success"

[<Test>]
let ``writeRunResultFile writes JSON for successful and ignored nodes`` () =
    withTempDir (fun root ->
        let path = Path.Combine(root, "nested", "run-result.json")
        let graph =
            buildGraph
                [ buildGraphNode "node-a" (Some "Root") "." "build"
                  buildGraphNode "node-b" (Some "App") "src/App" "test"
                  buildGraphNode "node-c" None "src/Unnamed" "build" ]
        let summary =
            buildSummary true
                [ "node-a", buildNodeInfo "." "build" (Runner.TaskStatus.Success DateTime.UtcNow) ]

        global.Program.writeRunResultFile path graph summary

        File.Exists(path) |> should equal true

        use doc = JsonDocument.Parse(File.ReadAllText(path))
        let json = doc.RootElement
        json.GetProperty("status").GetString() |> shouldEqual "success"
        json.GetProperty("targets").EnumerateArray() |> Seq.map (fun item -> item.GetString()) |> Seq.toList |> should equal [ "build"; "test" ]
        let results = json.GetProperty("results")
        results.GetProperty("root:build").GetString() |> shouldEqual "success"
        results.GetProperty("app:test").GetString() |> shouldEqual "ignored"
        hasJsonProperty results "unnamed:build" |> should equal false)

[<Test>]
let ``writeRunResultFile writes JSON for failed summary and optional writer skips None`` () =
    withTempDir (fun root ->
        let path = Path.Combine(root, "run-result.json")
        let missingPath = Path.Combine(root, "missing.json")
        let graph =
            buildGraph
                [ buildGraphNode "node-a" (Some "Root") "." "build"
                  buildGraphNode "node-b" (Some "App") "src/App" "test"
                  buildGraphNode "node-c" None "src/Unnamed" "build" ]
        let summary =
            buildSummary false
                [ "node-a", buildNodeInfo "." "build" (Runner.TaskStatus.Failure (DateTime.UtcNow, "failed")) ]

        global.Program.writeRunResultFile path graph summary
        global.Program.tryWriteRunResultFile None graph summary

        File.Exists(path) |> should equal true
        File.Exists(missingPath) |> should equal false

        use doc = JsonDocument.Parse(File.ReadAllText(path))
        let json = doc.RootElement
        json.GetProperty("status").GetString() |> shouldEqual "failure"
        let results = json.GetProperty("results")
        results.GetProperty("root:build").GetString() |> shouldEqual "failure"
        results.GetProperty("app:test").GetString() |> shouldEqual "ignored"
        hasJsonProperty results "unnamed:build" |> should equal false)

[<Test>]
let ``writeImpactResultFile writes JSON`` () =
    withTempDir (fun root ->
        let path = Path.Combine(root, "impact", "impact-result.json")
        let impactResult: global.Program.ImpactResult =
            { Base = "base-sha"
              Head = "head-sha"
              Targets = [ "build" ]
              Impacts = Map [ "app:build", "changed" ] }

        global.Program.writeImpactResultFile path impactResult

        File.Exists(path) |> should equal true

        use doc = JsonDocument.Parse(File.ReadAllText(path))
        let json = doc.RootElement
        json.GetProperty("base").GetString() |> shouldEqual "base-sha"
        json.GetProperty("head").GetString() |> shouldEqual "head-sha"
        json.GetProperty("impacts").GetProperty("app:build").GetString() |> shouldEqual "changed")

[<Test>]
let ``buildImpactResult reports changed target and parent hashes`` () =
    let currentGraph =
        buildGraph
            [ { buildGraphNode "lib-build" (Some "Lib") "src/Lib" "build" with
                    GraphDef.Node.TargetHash = "hash-lib-current" }
              { buildGraphNode "app-build" (Some "App") "src/App" "build" with
                    GraphDef.Node.TargetHash = "hash-app-current"
                    GraphDef.Node.Dependencies = Set [ "lib-build" ] }
              { buildGraphNode "batch" None ".terrabuild" "build" with
                    GraphDef.Node.Dependencies = Set [ "app-build" ] } ]

    let baseGraph =
        { Contracts.CommitGraph.Repository = "acme/repo"
          Contracts.CommitGraph.Commit = "base-sha"
          Contracts.CommitGraph.GraphHash = "graph-base"
          Contracts.CommitGraph.Nodes =
            [ { Contracts.BuildGraphNode.Id = "lib-build"
                ProjectId = "lib"
                ProjectName = Some "Lib"
                ProjectDir = "src/Lib"
                Target = "build"
                Phase = None
                ProjectHash = "project-lib"
                TargetHash = "hash-lib-base"
                Dependencies = []
                PhaseDependencies = []
                Transformations = []
                TargetHashResolved = Some true
                Artifacts = "Workspace"
                Build = "Auto"
                Batch = "Single"
                Action = "Exec"
                Required = true
                IsBatchNode = false }
              { Contracts.BuildGraphNode.Id = "app-build"
                ProjectId = "app"
                ProjectName = Some "App"
                ProjectDir = "src/App"
                Target = "build"
                Phase = None
                ProjectHash = "project-app"
                TargetHash = "hash-app"
                Dependencies = [ "lib-build" ]
                PhaseDependencies = []
                Transformations = []
                TargetHashResolved = Some true
                Artifacts = "Workspace"
                Build = "Auto"
                Batch = "Single"
                Action = "Exec"
                Required = true
                IsBatchNode = false } ] }

    let impactResult =
        global.Program.buildImpactResult "base-sha"
                                         { Sha = "head-sha"
                                           Message = "head"
                                           Author = "dev"
                                           Email = "dev@example.com"
                                           Timestamp = DateTime.UtcNow }
                                         (Set [ "build" ])
                                         currentGraph
                                         baseGraph

    impactResult.Base |> should equal "base-sha"
    impactResult.Head |> should equal "head-sha"
    impactResult.Impacts["lib:build"] |> should equal "changed"
    impactResult.Impacts["app:build"] |> should equal "changed"

[<Test>]
let ``buildImpactResult marks missing base nodes as changed and skips unnamed nodes`` () =
    let currentGraph =
        buildGraph
            [ buildGraphNode "root-build" (Some "Root") "." "build"
              buildGraphNode "unnamed-build" None "src/Generated" "build" ]

    let baseGraph =
        { Contracts.CommitGraph.Repository = "acme/repo"
          Contracts.CommitGraph.Commit = "base-sha"
          Contracts.CommitGraph.GraphHash = "graph-base"
          Contracts.CommitGraph.Nodes = [] }

    let impactResult =
        global.Program.buildImpactResult "base-sha"
                                         { Sha = "head-sha"
                                           Message = "head"
                                           Author = "dev"
                                           Email = "dev@example.com"
                                           Timestamp = DateTime.UtcNow }
                                         (Set [ "build" ])
                                         currentGraph
                                         baseGraph

    impactResult.Impacts.Count |> should equal 1
    impactResult.Impacts["root:build"] |> should equal "changed"

let private storedImpactNode (node: GraphDef.Node) : Contracts.BuildGraphNode =
    { Id = node.Id
      ProjectId = node.ProjectId
      ProjectName = node.ProjectName
      ProjectDir = node.ProjectDir
      Target = node.Target
      Phase = node.Phase
      ProjectHash = node.ProjectHash
      TargetHash = node.TargetHash
      Dependencies = Set.toList node.Dependencies
      PhaseDependencies = Set.toList node.PhaseDependencies
      Transformations = []
      TargetHashResolved = Some true
      Artifacts = "Workspace"
      Build = "Auto"
      Batch = "Single"
      Action = "Exec"
      Required = true
      IsBatchNode = false }

let private compareImpact currentNodes baseNodes =
    let baseline: Contracts.CommitGraph =
        { Repository = "acme/repo"
          Commit = "base-sha"
          GraphHash = "base-graph"
          Nodes = baseNodes }
    global.Program.buildImpactResult "base-sha"
        { Sha = "head-sha"; Message = "head"; Author = "dev"; Email = "dev@example.com"; Timestamp = DateTime.UtcNow }
        (Set [ "build" ]) (buildGraph currentNodes) baseline

[<TestCase("added")>]
[<TestCase("removed")>]
[<TestCase("modified")>]
[<TestCase("unchanged")>]
let ``impact compares additions removals modifications and equality symmetrically`` change =
    let node = buildGraphNode "app-build" (Some "App") "src/App" "build"
    let current =
        if change = "removed" then []
        elif change = "modified" then [ { node with TargetHash = "new-hash" } ]
        else [ node ]
    let baseline = if change = "added" then [] else [ storedImpactNode node ]
    let result = compareImpact current baseline
    result.Impacts |> should equal (if change = "unchanged" then Map.empty else Map [ "app:build", "changed" ])

[<TestCase(true)>]
[<TestCase(false)>]
let ``impact reports a hash added or removed within a project target`` adding =
    let first = buildGraphNode "app-build-first" (Some "App") "src/App" "build"
    let second = buildGraphNode "app-build-second" (Some "App") "src/App" "build"
    let before, after = if adding then [ first ], [ first; second ] else [ first; second ], [ first ]
    (compareImpact after (before |> List.map storedImpactNode)).Impacts
    |> should equal (Map [ "app:build", "changed" ])

[<Test>]
let ``impact reports renamed projects as a removal and addition`` () =
    let before = buildGraphNode "app-build" (Some "Old") "src/App" "build"
    let after = { before with ProjectName = Some "New" }
    (compareImpact [ after ] [ storedImpactNode before ]).Impacts
    |> should equal (Map [ "old:build", "changed"; "new:build", "changed" ])

[<TestCase("unresolved")>]
[<TestCase("legacy")>]
[<TestCase("blank")>]
let ``impact rejects incomparable baseline hashes instead of inventing changes`` kind =
    let node = buildGraphNode "app-build" (Some "App") "src/App" "build"
    let baseline =
        { storedImpactNode node with
            TargetHashResolved = if kind = "legacy" then None else Some (kind <> "unresolved")
            TargetHash = if kind = "blank" then "" else node.TargetHash }
    let error = Assert.Throws<Errors.TerrabuildException>(fun () -> compareImpact [ node ] [ baseline ] |> ignore) |> nonNull
    error.Message |> should contain "resolved target hash"

[<Test>]
let ``impact ignores synthetic batches and unnamed projects on both sides`` () =
    let batch = buildGraphNode "batch" (Some "Batch") ".terrabuild" "build"
    let unnamed = buildGraphNode "unnamed" None "src/Generated" "build"
    let stored = { storedImpactNode batch with IsBatchNode = true; TargetHashResolved = None }
    (compareImpact [] [ stored; storedImpactNode unnamed ]).Impacts |> should equal Map.empty<string, string>
    let current = { buildGraph [ batch; unnamed ] with Batches = Map [ batch.Id, Set.empty ] }
    let result = global.Program.buildImpactResult "base"
                    { Sha = "head"; Message = ""; Author = "dev"; Email = "dev@example.com"; Timestamp = DateTime.UtcNow }
                    (Set [ "build" ]) current
                    { Repository = "repo"; Commit = "base"; GraphHash = "graph"; Nodes = [] }
    result.Impacts |> should equal Map.empty<string, string>


[<Test>]
let ``CLI accepts explicit cached output restoration`` () =
    let parser = ArgumentParser.Create<CLI.TerrabuildArgs>(programName = "terrabuild")
    let result = parser.ParseCommandLine([| "run"; "test"; "--restore"; "--project"; "api" |], raiseOnUsage = true)
    result.GetResult(TerrabuildArgs.Run).Contains(RunArgs.Restore) |> should equal true

[<Test>]
let ``run restore materializes cached reports without executing tests or restoring build prerequisites`` () =
    if OperatingSystem.IsWindows() then Assert.Ignore("Fixture uses a POSIX shell")
    withTempDir (fun root ->
        let app = Path.Combine(root, "app")
        let home = Path.Combine(root, "home")
        Directory.CreateDirectory(app) |> ignore
        File.WriteAllText(Path.Combine(root, "WORKSPACE"), "workspace { engine = ~host }\ntarget build { }\ntarget test { }\n")
        File.WriteAllText(Path.Combine(app, "PROJECT"), """
project fixture { }
target build {
    artifacts = ~managed
    outputs = [ "bin/" ]
    @shell sh { args = "build.sh" }
}
target test {
    artifacts = ~managed
    outputs = [ "TestResults/" ]
    depends_on = [ target.build ]
    @shell sh { args = "test.sh" }
}
        """)
        File.WriteAllText(Path.Combine(app, "build.sh"), "mkdir -p bin\nprintf tool > bin/tool\nprintf build >> ../build-count\n")
        File.WriteAllText(Path.Combine(app, "test.sh"), "test -f bin/tool || exit 9\nmkdir -p TestResults\nprintf report > TestResults/result.xml\nprintf test >> ../test-count\n")
        for args in [ "init -q"; "add WORKSPACE app"; "-c user.name=Test -c user.email=test@example.com commit -qm fixture" ] do
            match Exec.execCaptureOutput root "git" args Map.empty with
            | Exec.Success _ -> ()
            | Exec.Error (message, _) -> Assert.Fail(message)
        let binary = typeof<CLI.RunArgs>.Assembly.Location
        let run flags =
            match Exec.execCaptureOutput root "dotnet" ($"\"{binary}\" run test --project fixture --local-only {flags}") (Map [ "HOME", home ]) with
            | Exec.Success _ -> ()
            | Exec.Error (message, code) -> Assert.Fail($"CLI failed ({code}): {message}")
        run ""
        Directory.Delete(Path.Combine(app, "TestResults"), true)
        Directory.Delete(Path.Combine(app, "bin"), true)
        run ""
        File.Exists(Path.Combine(app, "TestResults/result.xml")) |> should equal false
        run "--restore"
        File.ReadAllText(Path.Combine(app, "TestResults/result.xml")) |> should equal "report"
        File.Exists(Path.Combine(app, "bin/tool")) |> should equal false
        File.ReadAllText(Path.Combine(root, "test-count")) |> should equal "test"
        File.ReadAllText(Path.Combine(root, "build-count")) |> should equal "build"
        File.AppendAllText(Path.Combine(app, "test.sh"), "\n# change test inputs\n")
        run "--restore"
        File.ReadAllText(Path.Combine(root, "test-count")) |> should equal "testtest"
    )
