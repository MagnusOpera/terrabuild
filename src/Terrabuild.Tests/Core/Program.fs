module Terrabuild.Tests.Core.Program
open System
open System.IO
open System.Text.Json
open Argu
open CLI
open FsUnit
open NUnit.Framework
open Contracts

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
      GraphDef.Graph.Batches = Map.empty
      GraphDef.Graph.Phases = Map.empty }

let private buildGraphNode id projectName projectDir target =
    { GraphDef.Node.Id = id
      GraphDef.Node.ProjectId = id
      GraphDef.Node.ProjectName = projectName
      GraphDef.Node.ProjectDir = projectDir
      GraphDef.Node.Target = target
      GraphDef.Node.Phase = None
      GraphDef.Node.Dependencies = Set.empty
      GraphDef.Node.PhaseDependencies = Set.empty
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

type private RecordingImpactApiClient() =
    let mutable lastLookup: (string * string * string) option = None

    member _.LastLookup = lastLookup

    interface IApiClient with
        member _.StartBuild() = ()
        member _.UploadBuildGraph _graphHash _environment _nodes = ()
        member _.CompleteBuild _success = ()
        member _.AddArtifact _project _projectName _target _projectHash _targetHash _files _success _startedAt _endedAt = ()
        member _.UseArtifact _projectHash _hash = ()
        member _.GetArtifact _path = Uri("https://example.invalid/artifact")
        member _.GetCommitGraph repository commit environment =
            lastLookup <- Some (repository, commit, environment)
            { CommitGraph.Repository = repository
              CommitGraph.Commit = commit
              CommitGraph.GraphHash = "graph"
              CommitGraph.Nodes = [] }

[<Test>]
let ``getImpactBaseGraph uses resolved environment key`` () =
    let api = RecordingImpactApiClient()
    let options: ConfigOptions.Options =
        { Workspace = "."
          HomeDir = "."
          TmpDir = "."
          SharedDir = "."
          WhatIf = false
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

    api.LastLookup |> should equal (Some ("acme/repo", "base-sha", "dev-staging"))

[<Test>]
let ``CLI parses out argument for run`` () =
    let parser = ArgumentParser.Create<CLI.TerrabuildArgs>(programName = "terrabuild")
    let result = parser.ParseCommandLine([| "run"; "build"; "--out"; "out.json" |], raiseOnUsage = true)
    let runArgs = result.GetResult(TerrabuildArgs.Run)

    runArgs.GetResult(RunArgs.Out) |> should equal "out.json"

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
        json.GetProperty("status").GetString() |> should equal "success"
        json.GetProperty("targets").EnumerateArray() |> Seq.map (fun item -> item.GetString()) |> Seq.toList |> should equal [ "build"; "test" ]
        let results = json.GetProperty("results")
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

        global.Program.writeRunResultFile path graph summary
        global.Program.tryWriteRunResultFile None graph summary

        File.Exists(path) |> should equal true
        File.Exists(missingPath) |> should equal false

        use doc = JsonDocument.Parse(File.ReadAllText(path))
        let json = doc.RootElement
        json.GetProperty("status").GetString() |> should equal "failure"
        let results = json.GetProperty("results")
        results.GetProperty("root:build").GetString() |> should equal "failure"
        results.GetProperty("app:test").GetString() |> should equal "ignored"
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
        json.GetProperty("base").GetString() |> should equal "base-sha"
        json.GetProperty("head").GetString() |> should equal "head-sha"
        json.GetProperty("impacts").GetProperty("app:build").GetString() |> should equal "changed")

[<Test>]
let ``buildImpactResult reports changed and dependency impacts from target hashes`` () =
    let currentGraph =
        buildGraph
            [ { buildGraphNode "lib-build" (Some "Lib") "src/Lib" "build" with
                    GraphDef.Node.TargetHash = "hash-lib-current" }
              { buildGraphNode "app-build" (Some "App") "src/App" "build" with
                    GraphDef.Node.TargetHash = "hash-app"
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
                ProjectHash = "project-lib"
                TargetHash = "hash-lib-base"
                Dependencies = []
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
                ProjectHash = "project-app"
                TargetHash = "hash-app"
                Dependencies = [ "lib-build" ]
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
    impactResult.Impacts["app:build"] |> should equal "dependency"

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
