module Terrabuild.Tests.Core.Runner
open System
open System.IO
open System.Collections.Generic
open FsUnit
open NUnit.Framework
open Contracts

let private buildNode id projectDir target action operations =
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
      GraphDef.Node.Operations = operations
      GraphDef.Node.Artifacts = GraphDef.ArtifactMode.Workspace
      GraphDef.Node.Build = GraphDef.BuildMode.Auto
      GraphDef.Node.Batch = GraphDef.BatchMode.Single
      GraphDef.Node.Action = action
      GraphDef.Node.Required = true }

let private baseOptions workspace =
    { ConfigOptions.Options.Workspace = workspace
      ConfigOptions.Options.HomeDir = workspace
      ConfigOptions.Options.TmpDir = workspace
      ConfigOptions.Options.SharedDir = workspace
      ConfigOptions.Options.WhatIf = false
      ConfigOptions.Options.Debug = false
      ConfigOptions.Options.MaxConcurrency = 2
      ConfigOptions.Options.Force = false
      ConfigOptions.Options.Retry = false
      ConfigOptions.Options.LocalOnly = true
      ConfigOptions.Options.StartedAt = DateTime.UtcNow
      ConfigOptions.Options.Targets = Set [ "build" ]
      ConfigOptions.Options.Configuration = None
      ConfigOptions.Options.Environment = None
      ConfigOptions.Options.LogTypes = []
      ConfigOptions.Options.Note = None
      ConfigOptions.Options.Label = None
      ConfigOptions.Options.Types = None
      ConfigOptions.Options.Labels = None
      ConfigOptions.Options.Projects = None
      ConfigOptions.Options.Variables = Map.empty
      ConfigOptions.Options.Engine = None
      ConfigOptions.Options.BranchOrTag = "main"
      ConfigOptions.Options.HeadCommit =
        { Commit.Sha = "deadbeef"
          Commit.Author = "test"
          Commit.Email = "test@example.com"
          Commit.Message = "test"
          Commit.Timestamp = DateTime.UtcNow }
      ConfigOptions.Options.CommitLog = []
      ConfigOptions.Options.Run = None }

let private withTempWorkspace action =
    let root = Path.Combine(Path.GetTempPath(), $"terrabuild-runner-tests-{Guid.NewGuid():N}")
    Directory.CreateDirectory(root) |> ignore
    let oldCurrentDir = Environment.CurrentDirectory
    Environment.CurrentDirectory <- root
    try
        action root
    finally
        Environment.CurrentDirectory <- oldCurrentDir
        if Directory.Exists(root) then
            Directory.Delete(root, true)

type private FakeEntry(root: string, id: string, completed: ResizeArray<string>) =
    let entryRoot = Path.Combine(root, id.Replace("/", "_"))
    let logsDir = Path.Combine(entryRoot, "logs")
    let outputsDir = Path.Combine(entryRoot, "outputs")
    let mutable logIndex = 0

    do
        Directory.CreateDirectory(logsDir) |> ignore
        Directory.CreateDirectory(outputsDir) |> ignore

    interface Cache.IEntry with
        member _.NextLogFile() =
            logIndex <- logIndex + 1
            Path.Combine(logsDir, $"step{logIndex}.log")

        member _.CompleteLogFile(_summary) = ()
        member _.Outputs = outputsDir
        member _.Logs = logsDir

        member _.Complete(_summary) =
            completed.Add(id)
            [ $"artifact-{id}" ]

type private FakeCache(root: string) =
    let completed = ResizeArray<string>()
    let entries = Dictionary<string, FakeEntry>()

    member _.Completed = completed |> Seq.toList

    interface Cache.ICache with
        member _.TryGetSummaryOnly _useRemote _id = None
        member _.TryGetSummary _useRemote _id = None

        member _.GetEntry _useRemote id =
            match entries.TryGetValue(id) with
            | true, entry -> entry :> Cache.IEntry
            | _ ->
                let entry = FakeEntry(root, id, completed)
                entries[id] <- entry
                entry :> Cache.IEntry

type private FakeApiClient() =
    let addCalls = ResizeArray<string * string * string * string * string list * bool>()
    let useCalls = ResizeArray<string * string>()

    member _.AddCalls = addCalls |> Seq.toList
    member _.UseCalls = useCalls |> Seq.toList

    interface Contracts.IApiClient with
        member _.StartBuild() = ()
        member _.CompleteBuild(_success) = ()
        member _.GetArtifact(_path) = Uri("https://example.invalid/artifact")

        member _.AddArtifact project target projectHash targetHash files success =
            addCalls.Add(project, target, projectHash, targetHash, files, success)

        member _.UseArtifact projectHash targetHash =
            useCalls.Add(projectHash, targetHash)

[<Test>]
let ``buildBatchSchedule flattens member labels in GitHub mode`` () =
    let memberA = buildNode "node-a" "apps/Api" "install" GraphDef.RunAction.Ignore []
    let memberB = buildNode "node-b" "libs/MagnusOpera.DbModels.Insights" "install" GraphDef.RunAction.Ignore []
    let batchNode = buildNode "batch-install" "." "install" GraphDef.RunAction.Ignore []
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
    let memberA = buildNode "node-a" "apps/Api" "install" GraphDef.RunAction.Ignore []
    let memberB = buildNode "node-b" "libs/MagnusOpera.DbModels.Insights" "install" GraphDef.RunAction.Ignore []
    let batchNode = buildNode "batch-install" "." "install" GraphDef.RunAction.Ignore []
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

[<TestCase("/usr/bin/true", true)>]
[<TestCase("/usr/bin/false", false)>]
let ``run keeps restored batch members as artifact reuses`` command expectedSuccess =
    withTempWorkspace (fun workspace ->
        let operation =
            { GraphDef.ContaineredShellOperation.Image = None
              GraphDef.ContaineredShellOperation.Platform = None
              GraphDef.ContaineredShellOperation.Cpus = None
              GraphDef.ContaineredShellOperation.Variables = Set.empty
              GraphDef.ContaineredShellOperation.Envs = Map.empty
              GraphDef.ContaineredShellOperation.MetaCommand = "test"
              GraphDef.ContaineredShellOperation.Command = command
              GraphDef.ContaineredShellOperation.Arguments = ""
              GraphDef.ContaineredShellOperation.ErrorLevel = 0 }

        let execMember = buildNode "member-exec" workspace "build" GraphDef.RunAction.Exec []
        let restoreMember = buildNode "member-restore" workspace "build" GraphDef.RunAction.Restore []
        let batchNode = buildNode "batch-build" "." "build" GraphDef.RunAction.Exec [ operation ]
        let graph =
            { GraphDef.Graph.Nodes =
                [ execMember.Id, execMember
                  restoreMember.Id, restoreMember
                  batchNode.Id, batchNode ] |> Map.ofList
              GraphDef.Graph.RootNodes = Set [ execMember.Id; restoreMember.Id ]
              GraphDef.Graph.Batches = Map [ batchNode.Id, Set [ execMember.Id; restoreMember.Id ] ] }

        let cache = FakeCache(workspace)
        let api = FakeApiClient()
        let summary = Runner.run (baseOptions workspace) (cache :> Cache.ICache) (Some (api :> Contracts.IApiClient)) graph

        cache.Completed |> should equal [ GraphDef.buildCacheKey execMember ]
        api.AddCalls.Length |> should equal 1
        api.AddCalls[0] |> should equal (
            execMember.ProjectDir,
            execMember.Target,
            execMember.ProjectHash,
            execMember.TargetHash,
            [ $"artifact-{GraphDef.buildCacheKey execMember}" ],
            expectedSuccess)
        api.UseCalls |> should equal [ (restoreMember.ProjectHash, restoreMember.TargetHash) ]

        match summary.Nodes[execMember.Id].Request with
        | Runner.TaskRequest.Exec -> ()
        | request -> Assert.Fail($"Expected exec request for exec member, got {request}")

        match summary.Nodes[restoreMember.Id].Request with
        | Runner.TaskRequest.Restore -> ()
        | request -> Assert.Fail($"Expected restore request for restored member, got {request}")

        summary.Nodes[execMember.Id].Status.IsSuccess |> should equal expectedSuccess
        summary.Nodes[restoreMember.Id].Status.IsSuccess |> should equal expectedSuccess)
