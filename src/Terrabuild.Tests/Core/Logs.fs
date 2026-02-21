module Terrabuild.Tests.Core.Logs
open System
open System.IO
open System.Text.RegularExpressions
open Collections
open FsUnit
open NUnit.Framework
open Cache

let private buildOptions (markdownFile: string) =
    { ConfigOptions.Options.Workspace = "."
      ConfigOptions.Options.HomeDir = "."
      ConfigOptions.Options.TmpDir = "."
      ConfigOptions.Options.SharedDir = "."
      ConfigOptions.Options.WhatIf = false
      ConfigOptions.Options.Debug = false
      ConfigOptions.Options.MaxConcurrency = 1
      ConfigOptions.Options.Force = false
      ConfigOptions.Options.Retry = false
      ConfigOptions.Options.LocalOnly = true
      ConfigOptions.Options.StartedAt = DateTime.UtcNow.AddMinutes(-1.0)
      ConfigOptions.Options.Targets = Set [ "build" ]
      ConfigOptions.Options.Configuration = None
      ConfigOptions.Options.Environment = None
      ConfigOptions.Options.LogTypes = [ Contracts.LogType.Markdown markdownFile ]
      ConfigOptions.Options.Note = None
      ConfigOptions.Options.Label = None
      ConfigOptions.Options.Types = None
      ConfigOptions.Options.Labels = None
      ConfigOptions.Options.Projects = None
      ConfigOptions.Options.Variables = Map.empty
      ConfigOptions.Options.Engine = None
      ConfigOptions.Options.BranchOrTag = "main"
      ConfigOptions.Options.HeadCommit =
        { Contracts.Commit.Sha = "sha"
          Contracts.Commit.Message = "msg"
          Contracts.Commit.Author = "author"
          Contracts.Commit.Email = "mail"
          Contracts.Commit.Timestamp = DateTime.UtcNow }
      ConfigOptions.Options.CommitLog = []
      ConfigOptions.Options.Run = None }

let private buildNode id projectDir projectHash targetHash =
    { GraphDef.Node.Id = id
      GraphDef.Node.ProjectId = id
      GraphDef.Node.ProjectName = None
      GraphDef.Node.ProjectDir = projectDir
      GraphDef.Node.Target = "build"
      GraphDef.Node.Dependencies = Set.empty
      GraphDef.Node.Outputs = Set.empty
      GraphDef.Node.ProjectHash = projectHash
      GraphDef.Node.TargetHash = targetHash
      GraphDef.Node.ClusterHash = Some "cluster"
      GraphDef.Node.Operations = []
      GraphDef.Node.Artifacts = GraphDef.ArtifactMode.Workspace
      GraphDef.Node.Build = GraphDef.BuildMode.Auto
      GraphDef.Node.Batch = GraphDef.BatchMode.Single
      GraphDef.Node.Action = GraphDef.RunAction.Exec
      GraphDef.Node.Required = true }

let private buildSummaryInfo () =
    { Runner.NodeInfo.Request = Runner.TaskRequest.Exec
      Runner.NodeInfo.Status = Runner.TaskStatus.Success DateTime.UtcNow
      Runner.NodeInfo.Project = "."
      Runner.NodeInfo.Target = "build"
      Runner.NodeInfo.ProjectHash = "ph"
      Runner.NodeInfo.TargetHash = "th" }

let private buildStep (logFile: string) =
    { OperationSummary.MetaCommand = "@dotnet build"
      OperationSummary.Command = "dotnet"
      OperationSummary.Arguments = "build"
      OperationSummary.Container = None
      OperationSummary.StartedAt = DateTime.UtcNow.AddSeconds(-2.0)
      OperationSummary.EndedAt = DateTime.UtcNow.AddSeconds(-1.0)
      OperationSummary.Duration = TimeSpan.FromSeconds(1.0)
      OperationSummary.Log = logFile
      OperationSummary.ExitCode = 0 }

let private buildTargetSummary (logFile: string) =
    { TargetSummary.Project = "."
      TargetSummary.Target = "build"
      TargetSummary.Operations = [ [ buildStep logFile ] ]
      TargetSummary.Outputs = None
      TargetSummary.IsSuccessful = true
      TargetSummary.StartedAt = DateTime.UtcNow.AddSeconds(-5.0)
      TargetSummary.EndedAt = DateTime.UtcNow.AddSeconds(-1.0)
      TargetSummary.Duration = TimeSpan.FromSeconds(4.0)
      TargetSummary.Cache = GraphDef.ArtifactMode.Workspace }

type private FakeCache(summaries: Map<string, Origin * TargetSummary>) =
    interface ICache with
        member _.TryGetSummaryOnly _ id = summaries |> Map.tryFind id
        member _.TryGetSummary _ id = summaries |> Map.tryFind id |> Option.map snd
        member _.GetEntry _ _ = raise (NotImplementedException("unused in Logs tests"))

let private markdownAnchorCount (content: string) =
    Regex.Matches(content, "## <a name=\"user-content-").Count

[<Test>]
let ``Markdown links batch members to single batch anchor`` () =
    let root = Path.Combine(Path.GetTempPath(), $"terrabuild-tests-{Guid.NewGuid():N}")
    Directory.CreateDirectory(root) |> ignore
    let markdownFile = Path.Combine(root, "summary.md")
    let logFile = Path.Combine(root, "batch.log")
    File.WriteAllText(logFile, "batch log")

    try
        let nodeA = buildNode "node-a" "src/a" "project-a" "target-a"
        let nodeB = buildNode "node-b" "src/b" "project-b" "target-b"
        let batchId = "batch-id-123"
        let graph =
            { GraphDef.Graph.Nodes = [ nodeA.Id, nodeA; nodeB.Id, nodeB ] |> Map.ofList
              GraphDef.Graph.RootNodes = Set [ nodeA.Id; nodeB.Id ]
              GraphDef.Graph.Batches = Map [ batchId, Set [ nodeA.Id; nodeB.Id ] ] }

        let summaries =
            [ nodeA; nodeB ]
            |> List.map (fun node ->
                let key = GraphDef.buildCacheKey node
                key, (Origin.Local, buildTargetSummary logFile))
            |> Map.ofList

        let runnerSummary =
            { Runner.Summary.Commit = "c"
              Runner.Summary.BranchOrTag = "main"
              Runner.Summary.StartedAt = DateTime.UtcNow.AddMinutes(-2.0)
              Runner.Summary.EndedAt = DateTime.UtcNow
              Runner.Summary.IsSuccess = true
              Runner.Summary.Targets = Set [ "build" ]
              Runner.Summary.Nodes = [ nodeA.Id, buildSummaryInfo (); nodeB.Id, buildSummaryInfo () ] |> Map.ofList }

        let logId = Guid.Parse("11111111-2222-3333-4444-555555555555")
        let options = buildOptions markdownFile
        let cache = FakeCache(summaries) :> ICache
        Logs.dumpLogs logId options cache graph runnerSummary

        let markdown = File.ReadAllText(markdownFile)
        let batchAnchor = Hash.md5 $"{logId} {batchId}" |> String.toLower
        let nodeAAnchor = Hash.md5 $"{logId} {nodeA.Id}" |> String.toLower
        let nodeBAnchor = Hash.md5 $"{logId} {nodeB.Id}" |> String.toLower

        markdown.Contains($"(#user-content-{batchAnchor})") |> should equal true
        markdown.Contains($"[build [batch:{batchId}]](#user-content-{batchAnchor})") |> should equal true
        markdown.Contains($"[build {nodeA.ProjectDir}]") |> should equal false
        markdown.Contains($"[build {nodeB.ProjectDir}]") |> should equal false
        markdown.Contains($"## <a name=\"user-content-{batchAnchor}\"></a>") |> should equal true
        markdown.Contains($"## <a name=\"user-content-{nodeAAnchor}\"></a>") |> should equal false
        markdown.Contains($"## <a name=\"user-content-{nodeBAnchor}\"></a>") |> should equal false
        markdownAnchorCount markdown |> should equal 1
    finally
        if Directory.Exists(root) then Directory.Delete(root, true)

[<Test>]
let ``Markdown keeps separate anchors for non batched nodes`` () =
    let root = Path.Combine(Path.GetTempPath(), $"terrabuild-tests-{Guid.NewGuid():N}")
    Directory.CreateDirectory(root) |> ignore
    let markdownFile = Path.Combine(root, "summary.md")
    let logFile = Path.Combine(root, "single.log")
    File.WriteAllText(logFile, "single log")

    try
        let nodeA = buildNode "node-a" "src/a" "project-a" "target-a"
        let nodeB = buildNode "node-b" "src/b" "project-b" "target-b"
        let graph =
            { GraphDef.Graph.Nodes = [ nodeA.Id, nodeA; nodeB.Id, nodeB ] |> Map.ofList
              GraphDef.Graph.RootNodes = Set [ nodeA.Id; nodeB.Id ]
              GraphDef.Graph.Batches = Map.empty }

        let summaries =
            [ nodeA; nodeB ]
            |> List.map (fun node ->
                let key = GraphDef.buildCacheKey node
                key, (Origin.Local, buildTargetSummary logFile))
            |> Map.ofList

        let runnerSummary =
            { Runner.Summary.Commit = "c"
              Runner.Summary.BranchOrTag = "main"
              Runner.Summary.StartedAt = DateTime.UtcNow.AddMinutes(-2.0)
              Runner.Summary.EndedAt = DateTime.UtcNow
              Runner.Summary.IsSuccess = true
              Runner.Summary.Targets = Set [ "build" ]
              Runner.Summary.Nodes = [ nodeA.Id, buildSummaryInfo (); nodeB.Id, buildSummaryInfo () ] |> Map.ofList }

        let logId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")
        let options = buildOptions markdownFile
        let cache = FakeCache(summaries) :> ICache
        Logs.dumpLogs logId options cache graph runnerSummary

        let markdown = File.ReadAllText(markdownFile)
        let nodeAAnchor = Hash.md5 $"{logId} {nodeA.Id}" |> String.toLower
        let nodeBAnchor = Hash.md5 $"{logId} {nodeB.Id}" |> String.toLower

        markdown.Contains($"[build {nodeA.ProjectDir}](#user-content-{nodeAAnchor})") |> should equal true
        markdown.Contains($"[build {nodeB.ProjectDir}](#user-content-{nodeBAnchor})") |> should equal true
        markdown.Contains($"## <a name=\"user-content-{nodeAAnchor}\"></a>") |> should equal true
        markdown.Contains($"## <a name=\"user-content-{nodeBAnchor}\"></a>") |> should equal true
        markdownAnchorCount markdown |> should equal 2
    finally
        if Directory.Exists(root) then Directory.Delete(root, true)
