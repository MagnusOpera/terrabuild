module Terrabuild.Tests.Core.Program
open System
open System.IO
open System.Text.Json
open Argu
open CLI
open FsUnit
open NUnit.Framework

let private buildNodeInfo project target status =
    { Runner.NodeInfo.Request = Runner.TaskRequest.Exec
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
      GraphDef.Graph.Batches = Map.empty }

let private buildGraphNode id projectName projectDir target =
    { GraphDef.Node.Id = id
      GraphDef.Node.ProjectId = id
      GraphDef.Node.ProjectName = projectName
      GraphDef.Node.ProjectDir = projectDir
      GraphDef.Node.Target = target
      GraphDef.Node.Dependencies = Set.empty
      GraphDef.Node.Outputs = Set.empty
      GraphDef.Node.ProjectHash = $"project-{id}"
      GraphDef.Node.TargetHash = $"target-{id}"
      GraphDef.Node.ClusterHash = None
      GraphDef.Node.Operations = []
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

[<Test>]
let ``CLI parses result argument`` () =
    let parser = ArgumentParser.Create<CLI.TerrabuildArgs>(programName = "terrabuild")
    let result = parser.ParseCommandLine([| "run"; "build"; "--result"; "out.json" |], raiseOnUsage = true)
    let runArgs = result.GetResult(TerrabuildArgs.Run)

    runArgs.GetResult(RunArgs.Result) |> should equal "out.json"

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

    let runResult = global.Program.buildRunResult graph (Some summary) summary.StartedAt summary.EndedAt

    runResult.Status |> should equal "success"
    runResult.Targets |> should equal [ "build"; "test" ]
    runResult.Impacts["root:build"] |> should equal "build"
    runResult.Impacts["terrabuild.common:build"] |> should equal "restore"
    runResult.Impacts["terrabuild.common.tests:test"] |> should equal "report"
    runResult.Results.Value["root:build"] |> should equal "success"
    runResult.Results.Value["terrabuild.common:build"] |> should equal "success"
    runResult.Results.Value["terrabuild.common.tests:test"] |> should equal "success"

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

    let runResult = global.Program.buildRunResult graph (Some summary) summary.StartedAt summary.EndedAt

    runResult.Status |> should equal "failure"
    runResult.Impacts["app:build"] |> should equal "build"
    runResult.Impacts["lib:build"] |> should equal "restore"
    runResult.Results.Value.Count |> should equal 2
    runResult.Results.Value["app:build"] |> should equal "failure"
    runResult.Results.Value["lib:build"] |> should equal "success"

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

    let runResult = global.Program.buildRunResult graph (Some summary) summary.StartedAt summary.EndedAt

    runResult.Status |> should equal "success"
    runResult.Impacts["root:build"] |> should equal "build"
    runResult.Impacts["app:test"] |> should equal "ignore"
    runResult.Impacts["lib:build"] |> should equal "restore"
    runResult.Results.Value.Count |> should equal 3
    runResult.Results.Value["root:build"] |> should equal "success"
    runResult.Results.Value["app:test"] |> should equal "ignored"
    runResult.Results.Value["lib:build"] |> should equal "success"

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

    let runResult = global.Program.buildRunResult graph (Some summary) summary.StartedAt summary.EndedAt

    runResult.Impacts["app:build"] |> should equal "build"
    runResult.Results.Value.Count |> should equal 1
    runResult.Results.Value["app:build"] |> should equal "success"

[<Test>]
let ``buildRunResult omits results for what-if and still reports impacts`` () =
    let startedAt = DateTime.Parse("2026-04-11T16:42:46.595161Z").ToUniversalTime()
    let endedAt = DateTime.Parse("2026-04-11T16:42:50.000000Z").ToUniversalTime()
    let graph =
        buildGraph
            [ buildGraphNode "node-a" (Some "App") "src/App" "build" |> withAction GraphDef.RunAction.Exec
              buildGraphNode "node-b" (Some "Lib") "src/Lib" "test" |> withAction GraphDef.RunAction.Restore
              buildGraphNode "node-c" None "src/Unnamed" "build" |> withAction GraphDef.RunAction.Exec ]

    let runResult = global.Program.buildRunResult graph None startedAt endedAt

    runResult.Status |> should equal "what-if"
    runResult.Impacts.Count |> should equal 2
    runResult.Impacts["app:build"] |> should equal "build"
    runResult.Impacts["lib:test"] |> should equal "restore"
    runResult.Results |> should equal None

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

        global.Program.writeRunResultFile path graph (Some summary) summary.StartedAt summary.EndedAt

        File.Exists(path) |> should equal true

        use doc = JsonDocument.Parse(File.ReadAllText(path))
        let json = doc.RootElement
        json.GetProperty("status").GetString() |> should equal "success"
        json.GetProperty("targets").EnumerateArray() |> Seq.map (fun item -> item.GetString()) |> Seq.toList |> should equal [ "build"; "test" ]
        let impacts = json.GetProperty("impacts")
        let results = json.GetProperty("results")
        impacts.GetProperty("root:build").GetString() |> should equal "ignore"
        impacts.GetProperty("app:test").GetString() |> should equal "ignore"
        results.GetProperty("root:build").GetString() |> should equal "success"
        results.GetProperty("app:test").GetString() |> should equal "ignored"
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

        global.Program.writeRunResultFile path graph (Some summary) summary.StartedAt summary.EndedAt
        global.Program.tryWriteRunResultFile None graph (Some summary) summary.StartedAt summary.EndedAt

        File.Exists(path) |> should equal true
        File.Exists(missingPath) |> should equal false

        use doc = JsonDocument.Parse(File.ReadAllText(path))
        let json = doc.RootElement
        json.GetProperty("status").GetString() |> should equal "failure"
        let impacts = json.GetProperty("impacts")
        let results = json.GetProperty("results")
        impacts.GetProperty("root:build").GetString() |> should equal "ignore"
        impacts.GetProperty("app:test").GetString() |> should equal "ignore"
        results.GetProperty("root:build").GetString() |> should equal "failure"
        results.GetProperty("app:test").GetString() |> should equal "ignored"
        hasJsonProperty results "unnamed:build" |> should equal false)

[<Test>]
let ``writeRunResultFile omits results node for what-if`` () =
    withTempDir (fun root ->
        let path = Path.Combine(root, "what-if.json")
        let startedAt = DateTime.Parse("2026-04-11T16:42:46.595161Z").ToUniversalTime()
        let endedAt = DateTime.Parse("2026-04-11T16:42:50.000000Z").ToUniversalTime()
        let graph =
            buildGraph
                [ buildGraphNode "node-a" (Some "App") "src/App" "build" |> withAction GraphDef.RunAction.Exec
                  buildGraphNode "node-b" (Some "Lib") "src/Lib" "test" |> withAction GraphDef.RunAction.Restore ]

        global.Program.writeRunResultFile path graph None startedAt endedAt

        use doc = JsonDocument.Parse(File.ReadAllText(path))
        let json = doc.RootElement
        json.GetProperty("status").GetString() |> should equal "what-if"
        let impacts = json.GetProperty("impacts")
        impacts.GetProperty("app:build").GetString() |> should equal "build"
        impacts.GetProperty("lib:test").GetString() |> should equal "restore"
        hasJsonProperty json "results" |> should equal false)
