module Terrabuild.Tests.Core.DotnetRemoteCache

open System
open System.IO
open System.Collections.Concurrent
open FsUnit
open NUnit.Framework
open Contracts
open GraphDef

type private ArtifactAddCall =
    { Project: string
      Target: string
      ProjectHash: string
      TargetHash: string
      Files: string list
      Success: bool }

type private RecordingCache(inner: Cache.ICache) =
    let summaryOnlyCalls = ConcurrentQueue<string>()
    let summaryCalls = ConcurrentQueue<string>()
    let entryCalls = ConcurrentQueue<string>()

    member _.SummaryOnlyCalls = summaryOnlyCalls.ToArray() |> Array.toList
    member _.SummaryCalls = summaryCalls.ToArray() |> Array.toList
    member _.EntryCalls = entryCalls.ToArray() |> Array.toList

    interface Cache.ICache with
        member _.TryGetSummaryOnly useRemote id =
            summaryOnlyCalls.Enqueue(id)
            inner.TryGetSummaryOnly useRemote id

        member _.TryGetSummary useRemote id =
            summaryCalls.Enqueue(id)
            inner.TryGetSummary useRemote id

        member _.GetEntry useRemote id =
            entryCalls.Enqueue(id)
            inner.GetEntry useRemote id

type private PhaseResult =
    { GraphNode: Graph
      GraphAction: Graph
      GraphCascade: Graph
      GraphBatch: Graph
      Summary: Runner.Summary
      Cache: RecordingCache
      Api: RecordingApiClient }

and private RecordingApiClient() =
    let addCalls = ConcurrentQueue<ArtifactAddCall>()
    let useCalls = ConcurrentQueue<string * string>()
    let graphUploads = ConcurrentQueue<string * BuildGraphNode list>()
    let lifecycle = ConcurrentQueue<string>()

    member _.AddCalls = addCalls.ToArray() |> Array.toList
    member _.UseCalls = useCalls.ToArray() |> Array.toList
    member _.GraphUploads = graphUploads.ToArray() |> Array.toList
    member _.Lifecycle = lifecycle.ToArray() |> Array.toList

    interface IApiClient with
        member _.StartBuild() =
            lifecycle.Enqueue("start")

        member _.UploadBuildGraph graphHash nodes =
            lifecycle.Enqueue("upload-graph")
            graphUploads.Enqueue(graphHash, nodes)

        member _.CompleteBuild _success =
            lifecycle.Enqueue("complete")

        member _.GetArtifact _path =
            Uri("https://example.invalid/artifact")

        member _.AddArtifact project target projectHash targetHash files success _startedAt _endedAt =
            addCalls.Enqueue(
                { Project = project
                  Target = target
                  ProjectHash = projectHash
                  TargetHash = targetHash
                  Files = files
                  Success = success })

        member _.UseArtifact projectHash targetHash =
            useCalls.Enqueue(projectHash, targetHash)

type private FolderStorage(root: string) =
    let pathFromId (id: string) =
        id.Split('/', StringSplitOptions.RemoveEmptyEntries)
        |> Array.fold (fun current segment -> Path.Combine(current, segment)) root

    interface IStorage with
        member _.Exists id =
            pathFromId id |> File.Exists

        member _.TryDownload id =
            let source = pathFromId id
            if File.Exists(source) then
                let target = Path.GetTempFileName()
                File.Copy(source, target, true)
                Some target
            else
                None

        member _.Upload id summaryFile =
            let target = pathFromId id
            match Path.GetDirectoryName(target) with
            | null -> ()
            | directory -> Directory.CreateDirectory(directory) |> ignore
            File.Copy(summaryFile, target, true)

        member _.Name = "FolderStorage"

let private writeFile (root: string) (path: string) (content: string) =
    let full = Path.Combine(root, path)
    match Path.GetDirectoryName(full) with
    | null -> ()
    | directory -> Directory.CreateDirectory(directory) |> ignore
    File.WriteAllText(full, content)

let private copyDirectory (source: string) (destination: string) =
    Directory.CreateDirectory(destination) |> ignore

    for file in Directory.GetFiles(source, "*", SearchOption.AllDirectories) do
        let relative = Path.GetRelativePath(source, file)
        let target = Path.Combine(destination, relative)
        match Path.GetDirectoryName(target) with
        | null -> ()
        | directory -> Directory.CreateDirectory(directory) |> ignore
        File.Copy(file, target, true)

let private runGit directory args =
    match Exec.execCaptureOutput directory "git" args Map.empty with
    | Exec.Success _ -> ()
    | Exec.Error (message, code) -> Assert.Fail($"git {args} failed with exit code {code}: {message}")

let private initializeGitRepository directory =
    runGit directory "init -q"
    runGit directory "config user.name terrabuild-tests"
    runGit directory "config user.email terrabuild@example.com"
    runGit directory "add ."
    runGit directory "commit -qm init"

let private withEnvironmentVariable name value action =
    let previous = Environment.GetEnvironmentVariable(name)
    Environment.SetEnvironmentVariable(name, value)
    try
        action ()
    finally
        Environment.SetEnvironmentVariable(name, previous)

let private withCurrentDirectory directory action =
    let previous = Environment.CurrentDirectory
    Environment.CurrentDirectory <- directory
    try
        action ()
    finally
        Environment.CurrentDirectory <- previous

let private withTempRoot action =
    let root = Path.Combine(Path.GetTempPath(), $"terrabuild-dotnet-remote-cache-{Guid.NewGuid():N}")
    Directory.CreateDirectory(root) |> ignore
    try
        action root
    finally
        if Directory.Exists(root) then
            try
                Directory.Delete(root, true)
            with
            | :? IOException
            | :? UnauthorizedAccessException -> ()

let private workspaceFile =
    """
workspace {}

variable buildnonce {
  default = "phase-1"
}

target build {
  depends_on = [ target.^build ]
}

extension @dotnet {
  image = "mcr.microsoft.com/dotnet/sdk:9.0.202"
}
"""

let private projectAProjectFile =
    """
project a {
  @dotnet { }
}

target build {
  artifacts = ~managed
  build = ~auto
  batch = ~never
  @dotnet restore {
    locked = false
    dependencies = true
  }
  @dotnet build {
    configuration = "Debug"
    dependencies = false
    args = "-p:CacheNonce=${var.buildnonce}"
  }
}
"""

let private projectBProjectFile =
    """
project b {
  @dotnet { }
}

target build {
  artifacts = ~managed
  build = ~auto
  batch = ~never
  @dotnet restore {
    locked = false
    dependencies = true
  }
  @dotnet build {
    configuration = "Debug"
    dependencies = true
  }
}
"""

let private projectBFile =
    """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.FileSystemGlobbing" Version="10.0.0" />
  </ItemGroup>
</Project>
"""

let private projectAFile =
    """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Serilog" Version="4.3.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../B/B.csproj" />
  </ItemGroup>
</Project>
"""

let private programV1 =
    """
using B;
using Serilog;

Log.Logger = new LoggerConfiguration().CreateLogger();
Log.Information("phase-1");
Console.WriteLine(Greeter.Create());
"""

let private classFileB =
    """
using Microsoft.Extensions.FileSystemGlobbing;
using System.Linq;

namespace B;

public static class Greeter
{
    public static string Create()
    {
        var matcher = new Matcher();
        matcher.AddInclude("*.cs");
        return $"matched-{matcher.GetResultsInFullPath(Directory.GetCurrentDirectory()).Count()}";
    }
}
"""

let private projectBLock =
    """
{
  "version": 1,
  "dependencies": {
    "net9.0": {
      "Microsoft.Extensions.FileSystemGlobbing": {
        "type": "Direct",
        "requested": "[10.0.0, )",
        "resolved": "10.0.0",
        "contentHash": "5hfVl/e+bx1px2UkN+1xXhd3hu7Ui6ENItBzckFaRDQXfr+SHT/7qrCDrlQekCF/PBtEu2vtk87U2+gDEF8EhQ=="
      }
    }
  }
}
"""

let private projectALock =
    """
{
  "version": 1,
  "dependencies": {
    "net9.0": {
      "Serilog": {
        "type": "Direct",
        "requested": "[4.3.0, )",
        "resolved": "4.3.0",
        "contentHash": "+cDryFR0GRhsGOnZSKwaDzRRl4MupvJ42FhCE4zhQRVanX0Jpg6WuCBk59OVhVDPmab1bB+nRykAnykYELA9qQ=="
      }
    }
  }
}
"""

let private createFixture root =
    writeFile root "WORKSPACE" workspaceFile
    writeFile root "A/PROJECT" projectAProjectFile
    writeFile root "B/PROJECT" projectBProjectFile
    writeFile root "A/A.csproj" projectAFile
    writeFile root "B/B.csproj" projectBFile
    writeFile root "A/Program.cs" programV1
    writeFile root "B/Greeter.cs" classFileB
    writeFile root "A/packages.lock.json" projectALock
    writeFile root "B/packages.lock.json" projectBLock

let private createOptions workspace variables =
    { ConfigOptions.Options.Workspace = workspace
      ConfigOptions.Options.HomeDir = Cache.createHome()
      ConfigOptions.Options.TmpDir = Cache.createTmp()
      ConfigOptions.Options.SharedDir = ".terrabuild"
      ConfigOptions.Options.WhatIf = false
      ConfigOptions.Options.Debug = false
      ConfigOptions.Options.MaxConcurrency = 2
      ConfigOptions.Options.Force = false
      ConfigOptions.Options.Retry = false
      ConfigOptions.Options.LocalOnly = false
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
      ConfigOptions.Options.Variables = variables
      ConfigOptions.Options.Engine = Some "docker"
      ConfigOptions.Options.BranchOrTag = "main"
      ConfigOptions.Options.Repository = "acme/repo"
      ConfigOptions.Options.HeadCommit =
        { Commit.Sha = "deadbeef"
          Commit.Author = "test"
          Commit.Email = "test@example.com"
          Commit.Message = "test"
          Commit.Timestamp = DateTime.UtcNow }
      ConfigOptions.Options.CommitLog = []
      ConfigOptions.Options.Run = None }

let private nugetPackagesDir homeRoot =
    Path.Combine(homeRoot, ".terrabuild", "home", ".nuget", "packages")

let private runPhase (storage: FolderStorage) workspace homeRoot variables =
    let api = RecordingApiClient()

    withEnvironmentVariable "HOME" homeRoot (fun () ->
        Cache.createDirectories()

        withCurrentDirectory workspace (fun () ->
            Directory.CreateDirectory(".terrabuild") |> ignore

            let cache =
                Cache.Cache(storage :> IStorage, None)
                |> RecordingCache
            let options = createOptions workspace variables
            let options, config = Configuration.read options
            let graphNode = GraphPipeline.Node.build options config
            let graphAction = GraphPipeline.Action.build options (cache :> Cache.ICache) graphNode
            let graphCascade = GraphPipeline.Cascade.build graphAction
            let graphBatch = GraphPipeline.Batch.build options config graphCascade
            let summary = Runner.run options (cache :> Cache.ICache) (Some (api :> IApiClient)) graphBatch

            { GraphNode = graphNode
              GraphAction = graphAction
              GraphCascade = graphCascade
              GraphBatch = graphBatch
              Summary = summary
              Cache = cache
              Api = api }))

let private findBuildNode (graph: Graph) projectName =
    graph.Nodes.Values
    |> Seq.find (fun node -> node.Target = "build" && node.ProjectName = Some projectName)

let private assertBuildRequest expected (summary: Runner.Summary) nodeId =
    match summary.Nodes[nodeId].Request, expected with
    | Runner.TaskRequest.Exec, Runner.TaskRequest.Exec -> ()
    | Runner.TaskRequest.Restore, Runner.TaskRequest.Restore -> ()
    | request, _ -> Assert.Fail($"Unexpected request for {nodeId}: {request}")

let private assertSucceeded (summary: Runner.Summary) nodeId =
    match summary.Nodes[nodeId].Status with
    | Runner.TaskStatus.Success _ -> ()
    | status -> Assert.Fail($"Expected success for {nodeId}, got {status}")

let private assertDotnetOperations expectedRestoreArgs expectedBuildArgs (node: Node) =
    node.Operations
    |> List.map (fun op -> op.MetaCommand, op.Command, op.Arguments)
    |> should equal
        [ ("@dotnet restore", "dotnet", expectedRestoreArgs)
          ("@dotnet build", "dotnet", expectedBuildArgs) ]

let private assertDockerSummary expectedRestoreArgs expectedBuildArgs (cache: Cache.ICache) cacheKey =
    let summary: Cache.TargetSummary =
        match cache.TryGetSummary true cacheKey with
        | Some summary -> summary
        | None -> failwithf "Expected cache summary for %s" cacheKey

    let ops = summary.Operations |> List.concat
    ops.Length |> should equal 2
    ops[0].Command |> should equal "docker"
    ops[1].Command |> should equal "docker"
    ops[0].Arguments |> should contain "--entrypoint dotnet"
    ops[0].Arguments |> should contain expectedRestoreArgs
    ops[1].Arguments |> should contain "--entrypoint dotnet"
    ops[1].Arguments |> should contain expectedBuildArgs

let private assertRemoteOrigin (cache: Cache.ICache) cacheKey =
    match cache.TryGetSummaryOnly true cacheKey with
    | Some (Cache.Origin.Remote, _) -> ()
    | Some (origin, _) -> Assert.Fail($"Expected remote origin for {cacheKey}, got {origin}")
    | None -> Assert.Fail($"Expected summary origin for {cacheKey}")

[<Test; Category("integration"); NonParallelizable>]
let ``dotnet remote cache restores project reference with empty local caches`` () =
    withTempRoot (fun root ->
        let fixture = Path.Combine(root, "fixture")
        let clone1 = Path.Combine(root, "clone-1")
        let clone2 = Path.Combine(root, "clone-2")
        let home1 = Path.Combine(root, "home-1")
        let home2 = Path.Combine(root, "home-2")
        let storageRoot = Path.Combine(root, "remote-storage")
        let storage = FolderStorage(storageRoot)

        createFixture fixture
        copyDirectory fixture clone1
        initializeGitRepository clone1

        Directory.Exists(nugetPackagesDir home1) |> should equal false
        let phase1 = runPhase storage clone1 home1 Map.empty
        let phase1A = findBuildNode phase1.GraphNode "a"
        let phase1B = findBuildNode phase1.GraphNode "b"
        let phase1ActionA = findBuildNode phase1.GraphAction "a"
        let phase1ActionB = findBuildNode phase1.GraphAction "b"

        phase1.GraphBatch.Batches.Count |> should equal 0
        assertDotnetOperations "restore" "build --no-dependencies --configuration Debug -p:CacheNonce=phase-1" phase1A
        assertDotnetOperations "restore" "build --configuration Debug" phase1B
        phase1ActionA.Action |> should equal RunAction.Exec
        phase1ActionB.Action |> should equal RunAction.Exec
        assertBuildRequest Runner.TaskRequest.Exec phase1.Summary phase1ActionA.Id
        assertBuildRequest Runner.TaskRequest.Exec phase1.Summary phase1ActionB.Id
        assertSucceeded phase1.Summary phase1ActionA.Id
        assertSucceeded phase1.Summary phase1ActionB.Id
        phase1.Summary.IsSuccess |> should equal true
        phase1.Api.AddCalls |> List.map (fun call -> call.Project) |> Set.ofList |> should equal (Set [ "A"; "B" ])
        phase1.Api.AddCalls |> List.forall (fun call -> call.Success) |> should equal true

        assertDockerSummary "restore" "build --no-dependencies --configuration Debug -p:CacheNonce=phase-1" (phase1.Cache :> Cache.ICache) (buildCacheKey phase1A)
        assertDockerSummary "restore" "build --configuration Debug" (phase1.Cache :> Cache.ICache) (buildCacheKey phase1B)

        copyDirectory fixture clone2
        initializeGitRepository clone2

        Directory.Exists(nugetPackagesDir home2) |> should equal false
        let phase2 = runPhase storage clone2 home2 (Map.ofList [ "buildnonce", "phase-2" ])
        let phase2A = findBuildNode phase2.GraphNode "a"
        let phase2B = findBuildNode phase2.GraphNode "b"
        let phase2ActionA = findBuildNode phase2.GraphAction "a"
        let phase2ActionB = findBuildNode phase2.GraphAction "b"

        phase1A.ProjectHash |> should equal phase2A.ProjectHash
        phase1B.ProjectHash |> should equal phase2B.ProjectHash
        phase1A.TargetHash = phase2A.TargetHash |> should equal false
        phase1B.TargetHash |> should equal phase2B.TargetHash
        buildCacheKey phase1A = buildCacheKey phase2A |> should equal false
        buildCacheKey phase1B |> should equal (buildCacheKey phase2B)

        assertDotnetOperations "restore" "build --no-dependencies --configuration Debug -p:CacheNonce=phase-2" phase2A
        assertDotnetOperations "restore" "build --configuration Debug" phase2B
        phase2ActionA.Action |> should equal RunAction.Exec
        phase2ActionB.Action |> should equal RunAction.Restore
        assertBuildRequest Runner.TaskRequest.Exec phase2.Summary phase2ActionA.Id
        assertBuildRequest Runner.TaskRequest.Restore phase2.Summary phase2ActionB.Id
        assertSucceeded phase2.Summary phase2ActionA.Id
        assertSucceeded phase2.Summary phase2ActionB.Id
        phase2.Summary.IsSuccess |> should equal true

        phase2.Api.UseCalls |> should contain (phase2B.ProjectHash, phase2B.TargetHash)
        phase2.Api.AddCalls.Length |> should equal 1
        phase2.Api.AddCalls[0].Project |> should equal "A"
        phase2.Cache.EntryCalls |> List.contains (buildCacheKey phase2B) |> should equal false
        phase2.Cache.EntryCalls |> should contain (buildCacheKey phase2A)
        phase2.Cache.SummaryOnlyCalls |> should contain (buildCacheKey phase2B)
        phase2.Cache.SummaryCalls |> should contain (buildCacheKey phase2B)
        assertRemoteOrigin (phase2.Cache :> Cache.ICache) (buildCacheKey phase2B)
        assertDockerSummary "restore" "build --configuration Debug" (phase2.Cache :> Cache.ICache) (buildCacheKey phase2B)
        assertDockerSummary "restore" "build --no-dependencies --configuration Debug -p:CacheNonce=phase-2" (phase2.Cache :> Cache.ICache) (buildCacheKey phase2A)

        File.Exists(Path.Combine(clone2, "B", "obj", "project.assets.json")) |> should equal true
        File.Exists(Path.Combine(clone2, "B", "bin", "Debug", "net9.0", "B.dll")) |> should equal true)
