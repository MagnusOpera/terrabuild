module Terrabuild.Tests.Core.ConfigurationGraph

open System
open System.IO
open Collections
open FsUnit
open NUnit.Framework
open Contracts
open GraphDef
open Errors

let private baseOptions workspace targets =
    { ConfigOptions.Options.Workspace = workspace
      ConfigOptions.Options.HomeDir = workspace
      ConfigOptions.Options.TmpDir = workspace
      ConfigOptions.Options.SharedDir = workspace
      ConfigOptions.Options.WhatIf = true
      ConfigOptions.Options.Debug = false
      ConfigOptions.Options.MaxConcurrency = 4
      ConfigOptions.Options.Force = true
      ConfigOptions.Options.Retry = false
      ConfigOptions.Options.LocalOnly = true
      ConfigOptions.Options.StartedAt = DateTime.UtcNow
      ConfigOptions.Options.Targets = targets
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
        { Contracts.Commit.Sha = "deadbeef"
          Contracts.Commit.Author = "test"
          Contracts.Commit.Email = "test@example.com"
          Contracts.Commit.Message = "test"
          Contracts.Commit.Timestamp = DateTime.UtcNow }
      ConfigOptions.Options.CommitLog = []
      ConfigOptions.Options.Run = None }

let private writeFile (root: string) (path: string) (content: string) =
    let full = Path.Combine(root, path)
    match Path.GetDirectoryName(full) with
    | null -> ()
    | directory when String.IsNullOrWhiteSpace(directory) -> ()
    | directory -> Directory.CreateDirectory(directory) |> ignore
    File.WriteAllText(full, content)

let private writeDotnetProject root dir name references =
    let refs =
        references
        |> List.map (fun includePath -> $"    <ProjectReference Include=\"{includePath}\" />")
        |> String.concat "\n"

    let itemGroup =
        if String.IsNullOrWhiteSpace(refs) then ""
        else $"\n  <ItemGroup>\n{refs}\n  </ItemGroup>"

    writeFile root $"{dir}/{name}.csproj" $"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>{itemGroup}
</Project>
"""

let private withTempWorkspace action =
    let root = Path.Combine(Path.GetTempPath(), $"terrabuild-tests-{Guid.NewGuid():N}")
    Directory.CreateDirectory(root) |> ignore
    let gitInit = System.Diagnostics.ProcessStartInfo("git", "init -q")
    gitInit.WorkingDirectory <- root
    gitInit.RedirectStandardError <- true
    gitInit.RedirectStandardOutput <- true
    match System.Diagnostics.Process.Start(gitInit) with
    | null -> raiseBugError "Failed to start git init process"
    | git ->
        use git = git
        git.WaitForExit()
    try
        action root
    finally
        if Directory.Exists(root) then
            Directory.Delete(root, true)

type private NoopEntry() =
    interface Cache.IEntry with
        member _.NextLogFile() = raiseBugError "Not expected in this test"
        member _.CompleteLogFile(_summary) = ()
        member _.Outputs = raiseBugError "Not expected in this test"
        member _.Logs = raiseBugError "Not expected in this test"
        member _.Complete(_summary) = []

type private NoopCache() =
    interface Cache.ICache with
        member _.TryGetSummaryOnly _useRemote _id = None
        member _.TryGetSummary _useRemote _id = None
        member _.GetEntry _useRemote _id = NoopEntry() :> Cache.IEntry

let private runPipeline options =
    let options, config = Configuration.read options
    let graphNode = GraphPipeline.Node.build options config
    let graphAction = GraphPipeline.Action.build options (NoopCache() :> Cache.ICache) graphNode
    let graphBatch = GraphPipeline.Batch.build options config graphAction
    options, config, graphNode, graphAction, graphBatch

let private assertKnownErrorContains (expected: string) action =
    try
        action()
        Assert.Fail("Expected TerrabuildException")
    with
    | :? TerrabuildException as ex ->
        let dumped = ex |> dumpKnownException |> String.concat "\n"
        dumped.Contains(expected) |> should equal true

[<Test>]
let ``Configuration pipeline keeps non-batch operations ungrouped`` () =
    withTempWorkspace (fun workspace ->
        writeFile workspace "WORKSPACE" """
workspace {}

target build {
  batch = ~single
}
"""
        writeFile workspace "src/a/PROJECT" """
project a { @shell {} }
target build {
  @shell echo { arguments = "a" }
}
"""
        writeFile workspace "src/b/PROJECT" """
project b { @shell {} }
target build {
  @shell echo { arguments = "b" }
}
"""

        let options = baseOptions workspace (Set [ "build" ])
        let _, _, graphNode, _, graphBatch = runPipeline options

        graphNode.Nodes.Count |> should equal 2
        graphNode.Nodes |> Map.values |> Seq.forall (fun node -> node.ClusterHash.IsNone) |> should equal true
        graphBatch.Batches.Count |> should equal 0
        graphBatch.Nodes.Count |> should equal graphNode.Nodes.Count)

[<Test>]
let ``Configuration pipeline keeps disconnected nodes unbatched in partition mode`` () =
    withTempWorkspace (fun workspace ->
        writeFile workspace "WORKSPACE" """
workspace {}

target build {
  batch = ~partition
}
"""
        writeFile workspace "src/a/PROJECT" """
project a {
  depends_on = [ project.b ]
  @dotnet {}
}
target build {
  @dotnet build {}
}
"""
        writeDotnetProject workspace "src/a" "a" [ "../b/b.csproj" ]

        writeFile workspace "src/b/PROJECT" """
project b { @dotnet {} }
target build {
  @dotnet build {}
}
"""
        writeDotnetProject workspace "src/b" "b" []

        writeFile workspace "src/c/PROJECT" """
project c { @dotnet {} }
target build {
  @dotnet build {}
}
"""
        writeDotnetProject workspace "src/c" "c" []

        let options = baseOptions workspace (Set [ "build" ])
        let _, _, _, _, graphBatch = runPipeline options

        graphBatch.Batches.Count |> should equal 0
        graphBatch.Nodes.Count |> should equal 3
        graphBatch.Nodes |> Map.values |> Seq.forall (fun node -> node.Operations.Head.Arguments.Contains(".slnx") |> not) |> should equal true)

[<Test>]
let ``Configuration pipeline creates single batch with all projects`` () =
    withTempWorkspace (fun workspace ->
        writeFile workspace "WORKSPACE" """
workspace {}

target build {
  batch = ~single
}
"""
        writeFile workspace "src/a/PROJECT" """
project a { @dotnet {} }
target build {
  @dotnet build {}
}
"""
        writeDotnetProject workspace "src/a" "a" []

        writeFile workspace "src/b/PROJECT" """
project b { @dotnet {} }
target build {
  @dotnet build {}
}
"""
        writeDotnetProject workspace "src/b" "b" []

        writeFile workspace "src/c/PROJECT" """
project c { @dotnet {} }
target build {
  @dotnet build {}
}
"""
        writeDotnetProject workspace "src/c" "c" []

        let options = baseOptions workspace (Set [ "build" ])
        let _, _, _, _, graphBatch = runPipeline options

        graphBatch.Batches.Count |> should equal 1
        let (batchId, members) = graphBatch.Batches |> Seq.head |> (|KeyValue|)
        members |> should equal (Set [ "workspace/path#src/a:build"; "workspace/path#src/b:build"; "workspace/path#src/c:build" ])
        let batchNode = graphBatch.Nodes[batchId]
        batchNode.Operations.Head.Arguments.Contains(".slnx") |> should equal true)

[<Test>]
let ``Configuration pipeline disables clustering when one step is non-batchable`` () =
    withTempWorkspace (fun workspace ->
        writeFile workspace "WORKSPACE" """
workspace {}

target build {
  batch = ~single
}
"""
        writeFile workspace "src/a/PROJECT" """
project a {
  @dotnet {}
  @shell {}
}
target build {
  @dotnet build {}
  @shell echo { arguments = "a" }
}
"""
        writeDotnetProject workspace "src/a" "a" []

        writeFile workspace "src/b/PROJECT" """
project b {
  @dotnet {}
  @shell {}
}
target build {
  @dotnet build {}
  @shell echo { arguments = "b" }
}
"""
        writeDotnetProject workspace "src/b" "b" []

        let options = baseOptions workspace (Set [ "build" ])
        let _, _, graphNode, _, graphBatch = runPipeline options

        graphNode.Nodes |> Map.values |> Seq.forall (fun node -> node.ClusterHash.IsNone) |> should equal true
        graphBatch.Batches.Count |> should equal 0)

[<Test>]
let ``Configuration read fails on invalid batch value`` () =
    withTempWorkspace (fun workspace ->
        writeFile workspace "WORKSPACE" """
workspace {}

target build {
  batch = ~invalid
}
"""
        writeFile workspace "src/a/PROJECT" """
project a { @shell {} }
target build {
  @shell echo { arguments = "a" }
}
"""

        let options = baseOptions workspace (Set [ "build" ])
        assertKnownErrorContains "invalid" (fun () -> Configuration.read options |> ignore))

[<Test>]
let ``Graph node build fails when target is not defined in workspace`` () =
    withTempWorkspace (fun workspace ->
        writeFile workspace "WORKSPACE" """
workspace {}

target build {
  batch = ~single
}
"""
        writeFile workspace "src/a/PROJECT" """
project a { @shell {} }
target build {
  @shell echo { arguments = "a" }
}
"""

        let options = baseOptions workspace (Set [ "unknown" ])
        let options, config = Configuration.read options
        (fun () -> GraphPipeline.Node.build options config |> ignore)
        |> should (throwWithMessage "Target unknown is not defined in WORKSPACE") typeof<TerrabuildException>)
