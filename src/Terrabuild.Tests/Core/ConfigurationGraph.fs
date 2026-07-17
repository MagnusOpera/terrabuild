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
      ConfigOptions.Options.Engine = ConfigOptions.Engine.Host
      ConfigOptions.Options.BranchOrTag = "main"
      ConfigOptions.Options.Repository = "acme/repo"
      ConfigOptions.Options.HeadCommit =
        { Sha = "deadbeef"
          Author = "test"
          Email = "test@example.com"
          Message = "test"
          Timestamp = DateTime.UtcNow }
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

let private buildTargetSummary isSuccessful =
    { Cache.TargetSummary.Project = "."
      Cache.TargetSummary.Target = "build"
      Cache.TargetSummary.Operations = []
      Cache.TargetSummary.Outputs = None
      Cache.TargetSummary.IsSuccessful = isSuccessful
      Cache.TargetSummary.StartedAt = DateTime.UtcNow.AddMinutes(-1.0)
      Cache.TargetSummary.EndedAt = DateTime.UtcNow
      Cache.TargetSummary.Duration = TimeSpan.FromSeconds(1.0)
      Cache.TargetSummary.Cache = ArtifactMode.Workspace }

type private SummaryCache(summaries: Map<string, Cache.TargetSummary>) =

    interface Cache.ICache with
        member _.TryGetSummaryOnly _useRemote id =
            summaries
            |> Map.tryFind id
            |> Option.map (fun summary -> Cache.Origin.Local, summary)

        member _.TryGetSummary _useRemote id =
            summaries |> Map.tryFind id

        member _.GetEntry _useRemote _id = NoopEntry() :> Cache.IEntry

let private successCache summaryIds =
    summaryIds
    |> Seq.map (fun id -> id, buildTargetSummary true)
    |> Map.ofSeq
    |> fun summaries -> SummaryCache(summaries) :> Cache.ICache

let private failedCache summaryIds =
    summaryIds
    |> Seq.map (fun id -> id, buildTargetSummary false)
    |> Map.ofSeq
    |> fun summaries -> SummaryCache(summaries) :> Cache.ICache

type private PipelineStages =
    { Options: ConfigOptions.Options
      Config: Configuration.Workspace
      FullGraph: Graph
      SourceGraph: Graph
      ResolvedGraph: Graph
      ActionGraph: Graph
      CascadedGraph: Graph
      FinalGraph: Graph }

let private runPipelineWithCache (cache: Cache.ICache) options =
    let options, config = Configuration.read options
    let fullGraph = GraphPipeline.Node.build options config |> GraphPipeline.Phase.build
    let sourceGraph = GraphPipeline.Selection.build options config fullGraph
    let resolvedGraph = GraphPipeline.Resolve.build options config sourceGraph
    let actionGraph = GraphPipeline.Action.build options cache resolvedGraph
    let cascadedGraph = GraphPipeline.Cascade.build actionGraph
    let finalGraph = GraphPipeline.Batch.build options config cascadedGraph
    { Options = options
      Config = config
      FullGraph = fullGraph
      SourceGraph = sourceGraph
      ResolvedGraph = resolvedGraph
      ActionGraph = actionGraph
      CascadedGraph = cascadedGraph
      FinalGraph = finalGraph }

let private runPipeline options =
    runPipelineWithCache (NoopCache() :> Cache.ICache) options

let private assertKnownErrorContainsAll (expectedMessages: string list) action =
    try
        action()
        Assert.Fail("Expected TerrabuildException")
    with
    | :? TerrabuildException as ex ->
        let dumped = ex |> dumpKnownException |> String.concat "\n"
        expectedMessages
        |> List.iter (fun expected -> Assert.That(dumped, Does.Contain(expected)))

let private assertKnownErrorContains (expected: string) action =
    assertKnownErrorContainsAll [ expected ] action

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
        let stages = runPipeline options

        stages.FullGraph.Nodes.Count |> should equal 2
        stages.FullGraph.Nodes |> Map.values |> Seq.forall (fun node -> node.ClusterHash.IsNone) |> should equal true
        stages.FinalGraph.Batches.Count |> should equal 0
        stages.FinalGraph.Nodes.Count |> should equal stages.FullGraph.Nodes.Count)

[<Test>]
let ``Configuration pipeline applies project selection before resolving actions`` () =
    withTempWorkspace (fun workspace ->
        writeFile workspace "WORKSPACE" """
workspace {}

target build {}
"""
        writeFile workspace "apps/app/PROJECT" """
project app { @shell {} }
target build {
  @shell echo { args = "app" }
}
"""
        writeFile workspace "libs/lib/PROJECT" """
project lib { @shell {} }
target build {
  @shell echo { args = "lib" }
}
"""

        let options =
            { baseOptions workspace (Set [ "build" ]) with
                ConfigOptions.Options.Projects = Some (Set [ "app" ]) }

        let stages = runPipeline options
        let appNodeId = "workspace/path#apps/app:build"
        let libNodeId = "workspace/path#libs/lib:build"
        let selectedNodeIds = Set [ appNodeId ]
        let ids (graph: Graph) = graph.Nodes |> Map.keys |> Set.ofSeq

        stages.Config.SelectedProjects |> should equal (Set [ "workspace/path#apps/app" ])
        ids stages.FullGraph |> should equal (Set [ appNodeId; libNodeId ])
        ids stages.SourceGraph |> should equal selectedNodeIds
        ids stages.ResolvedGraph |> should equal selectedNodeIds
        ids stages.ActionGraph |> should equal selectedNodeIds
        ids stages.CascadedGraph |> should equal selectedNodeIds
        ids stages.FinalGraph |> should equal selectedNodeIds
        stages.FinalGraph.Nodes[appNodeId].Operations.Head.Arguments |> should equal "app")

[<Test>]
let ``Graph node stage expands current and upstream target dependencies`` () =
    withTempWorkspace (fun workspace ->
        writeFile workspace "WORKSPACE" """
workspace {}

target gen {}

target build {
  depends_on = [ target.gen
                 target.^build ]
}
"""
        writeFile workspace "apps/app/PROJECT" """
project app {
  depends_on = [ project.lib ]
  @shell {}
}
target gen {
  @shell echo { args = "gen" }
}
target build {
  @shell echo { args = "app" }
}
"""
        writeFile workspace "libs/lib/PROJECT" """
project lib { @shell {} }
target build {
  @shell echo { args = "lib" }
}
"""

        let stages = runPipeline (baseOptions workspace (Set [ "build" ]))
        let appBuild = "workspace/path#apps/app:build"
        let appGen = "workspace/path#apps/app:gen"
        let libBuild = "workspace/path#libs/lib:build"

        stages.FullGraph.Nodes[appBuild].Dependencies |> should equal (Set [ appGen; libBuild ])
        stages.FullGraph.Nodes[appGen].Dependencies |> should equal Set.empty<string>
        stages.FullGraph.Nodes[libBuild].Dependencies |> should equal Set.empty<string>
        stages.FullGraph.RootNodes |> should equal (Set [ appBuild ]))

[<Test>]
let ``Resolve stage honors explicit artifact mode and clears non-cacheable outputs`` () =
    withTempWorkspace (fun workspace ->
        writeFile workspace "WORKSPACE" """
workspace {}

target build {
  artifacts = ~none
  outputs = [ "dist/" ]
}
"""
        writeFile workspace "apps/app/PROJECT" """
project app { @shell {} }
target build {
  @shell echo { args = "build" }
}
"""

        let stages = runPipeline (baseOptions workspace (Set [ "build" ]))
        let node = stages.ResolvedGraph.Nodes["workspace/path#apps/app:build"]

        node.Operations.Length |> should equal 1
        node.Operations.Head.MetaCommand |> should equal "@shell echo"
        node.Artifacts |> should equal ArtifactMode.None
        node.Outputs |> should equal Set.empty<string>
        node.ClusterHash |> should equal None)

[<Test>]
let ``Configuration pipeline includes repository in project hash`` () =
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

        let firstOptions =
            { baseOptions workspace (Set [ "build" ]) with
                ConfigOptions.Options.Repository = "acme/repo-a" }
        let secondOptions =
            { baseOptions workspace (Set [ "build" ]) with
                ConfigOptions.Options.Repository = "acme/repo-b" }

        let firstStages = runPipeline firstOptions
        let secondStages = runPipeline secondOptions
        let firstNode = firstStages.FullGraph.Nodes["workspace/path#src/a:build"]
        let secondNode = secondStages.FullGraph.Nodes["workspace/path#src/a:build"]

        firstNode.ProjectHash = secondNode.ProjectHash |> should equal false)

[<Test>]
let ``Configuration pipeline normalizes equivalent repository identities in project hash`` () =
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

        let firstOptions =
            { baseOptions workspace (Set [ "build" ]) with
                ConfigOptions.Options.Repository = "git@github.com:acme/repo.git" }
        let secondOptions =
            { baseOptions workspace (Set [ "build" ]) with
                ConfigOptions.Options.Repository = "acme/repo" }

        let firstStages = runPipeline firstOptions
        let secondStages = runPipeline secondOptions
        let firstNode = firstStages.FullGraph.Nodes["workspace/path#src/a:build"]
        let secondNode = secondStages.FullGraph.Nodes["workspace/path#src/a:build"]

        firstNode.ProjectHash |> should equal secondNode.ProjectHash)

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
        let stages = runPipeline options

        stages.FinalGraph.Batches.Count |> should equal 0
        stages.FinalGraph.Nodes.Count |> should equal 3
        stages.FinalGraph.Nodes |> Map.values |> Seq.forall (fun node -> node.Operations.Head.Arguments.Contains(".slnx") |> not) |> should equal true)

[<Test>]
let ``Configuration pipeline resolves project map lookups for current project version`` () =
    withTempWorkspace (fun workspace ->
        writeFile workspace "WORKSPACE" """
workspace {}

target build {}
"""
        writeFile workspace "src/a/PROJECT" """
project a { @shell {} }
target build {
  @shell echo { args = "${project.[terrabuild.project].version}" }
}
"""

        let options = baseOptions workspace (Set [ "build" ])
        let stages = runPipeline options
        let node = stages.ResolvedGraph.Nodes["workspace/path#src/a:build"]

        node.Operations.Head.Arguments |> should equal node.ProjectHash)

[<Test>]
let ``Configuration pipeline does not create dependency for static current project version`` () =
    withTempWorkspace (fun workspace ->
        writeFile workspace "WORKSPACE" """
workspace {}

target build {}
"""
        writeFile workspace "src/a/PROJECT" """
project a { @shell {} }
target build {
  @shell echo { args = "${project.a.version}" }
}
"""

        let options = baseOptions workspace (Set [ "build" ])
        let stages = runPipeline options
        let node = stages.ResolvedGraph.Nodes["workspace/path#src/a:build"]

        node.Operations.Head.Arguments |> should equal node.ProjectHash)

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
        let stages = runPipeline options

        stages.FinalGraph.Batches.Count |> should equal 1
        let (batchId, members) = stages.FinalGraph.Batches |> Seq.head |> (|KeyValue|)
        members |> should equal (Set [ "workspace/path#src/a:build"; "workspace/path#src/b:build"; "workspace/path#src/c:build" ])
        let batchNode = stages.FinalGraph.Nodes[batchId]
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
        let stages = runPipeline options

        stages.FullGraph.Nodes |> Map.values |> Seq.forall (fun node -> node.ClusterHash.IsNone) |> should equal true
        stages.ResolvedGraph.Nodes |> Map.values |> Seq.forall (fun node -> node.ClusterHash.IsNone) |> should equal true
        stages.FinalGraph.Batches.Count |> should equal 0)

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
let ``Configuration read reports undefined extension with project context`` () =
    withTempWorkspace (fun workspace ->
        writeFile workspace "WORKSPACE" """
workspace {}

target build {}
"""
        writeFile workspace "src/a/PROJECT" """
project a {}
target build {
  @missing run {}
}
"""

        let options = baseOptions workspace (Set [ "build" ])
        assertKnownErrorContainsAll
            [ "Error while parsing project 'src/a'"
              "Extension @missing is not defined" ]
            (fun () -> Configuration.read options |> ignore))

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

[<Test>]
let ``Graph node build reports circular target dependencies`` () =
    withTempWorkspace (fun workspace ->
        writeFile workspace "WORKSPACE" """
workspace {}

target build {
  depends_on = [ target.test ]
}

target test {
  depends_on = [ target.build ]
}
"""
        writeFile workspace "src/a/PROJECT" """
project a { @shell {} }
target build {
  @shell echo { args = "build" }
}
target test {
  @shell echo { args = "test" }
}
"""

        let options = baseOptions workspace (Set [ "build" ])
        let options, config = Configuration.read options

        assertKnownErrorContains
            "Circular target dependency detected: workspace/path#src/a:build -> workspace/path#src/a:test -> workspace/path#src/a:build"
            (fun () -> GraphPipeline.Node.build options config |> ignore))

[<Test>]
let ``Action pipeline excludes cached selected roots from runnable roots`` () =
    withTempWorkspace (fun workspace ->
        writeFile workspace "WORKSPACE" """
workspace {}

target gen {
  outputs = []
  build = ~lazy
  artifacts = ~workspace
}

target build {
  outputs = []
  artifacts = ~workspace
  depends_on = [ target.gen ]
}
"""
        writeFile workspace "src/a/PROJECT" """
project a { @shell {} }
target gen {
  @shell echo { arguments = "gen" }
}
target build {
  @shell echo { arguments = "build" }
}
"""

        let options = { baseOptions workspace (Set [ "build" ]) with Force = false }
        let uncachedStages = runPipeline options
        let buildNode = uncachedStages.ResolvedGraph.Nodes["workspace/path#src/a:build"]
        let genNode = uncachedStages.ResolvedGraph.Nodes["workspace/path#src/a:gen"]
        let cachedIds = Set [ GraphDef.buildCacheKey buildNode; GraphDef.buildCacheKey genNode ]
        let stages = runPipelineWithCache (successCache cachedIds) options

        stages.ActionGraph.RootNodes |> should equal Set.empty<string>
        stages.ActionGraph.Nodes[buildNode.Id].Action |> should equal RunAction.Restore
        stages.ActionGraph.Nodes[genNode.Id].Action |> should equal RunAction.Restore)

[<Test>]
let ``Action pipeline keeps failed cached selected roots schedulable as summaries`` () =
    withTempWorkspace (fun workspace ->
        writeFile workspace "WORKSPACE" """
workspace {}

target build {
  outputs = []
  artifacts = ~workspace
}
"""
        writeFile workspace "src/a/PROJECT" """
project a { @shell {} }
target build {
  @shell echo { args = "build" }
}
"""

        let options = { baseOptions workspace (Set [ "build" ]) with Force = false }
        let uncachedStages = runPipeline options
        let node = uncachedStages.ResolvedGraph.Nodes["workspace/path#src/a:build"]
        let cache = failedCache (Set [ GraphDef.buildCacheKey node ])
        let stages = runPipelineWithCache cache options

        stages.ActionGraph.Nodes[node.Id].Action |> should equal RunAction.Summary
        stages.ActionGraph.RootNodes |> should equal (Set [ node.Id ])
        stages.CascadedGraph.RootNodes |> should equal (Set [ node.Id ])
        stages.FinalGraph.RootNodes |> should equal (Set [ node.Id ]))

[<Test>]
let ``Action pipeline excludes selected lazy roots when they must execute`` () =
    withTempWorkspace (fun workspace ->
        writeFile workspace "WORKSPACE" """
workspace {}

target gen {
  outputs = []
  build = ~lazy
  artifacts = ~workspace
}
"""
        writeFile workspace "src/a/PROJECT" """
project a { @shell {} }
target gen {
  @shell echo { arguments = "gen" }
}
"""

        let options = { baseOptions workspace (Set [ "gen" ]) with Force = false }
        let stages = runPipeline options
        let genNode = stages.ActionGraph.Nodes["workspace/path#src/a:gen"]

        genNode.Action |> should equal RunAction.Exec
        stages.ActionGraph.RootNodes |> should equal Set.empty<string>)

[<Test>]
let ``Phase graph enlists transitive prerequisite targets without selecting phase peers`` () =
    withTempWorkspace (fun workspace ->
        writeFile workspace "WORKSPACE" """
workspace {}

phase toolchains {}
phase empty { depends_on = [ phase.toolchains ] }
phase application { depends_on = [ phase.empty ] }

target prepare {}
target dist {}
target build { phase = phase.application }
"""
        writeFile workspace "tools/pnpm/PROJECT" """
project pnpm { @shell {} }
target prepare { @shell echo { args = "prepare pnpm" } }
target dist {
  phase = phase.toolchains
  depends_on = [ target.prepare ]
  @shell echo { args = "dist pnpm" }
}
"""
        writeFile workspace "apps/app/PROJECT" """
project app { @shell {} }
target build { @shell echo { args = "build app" } }
"""
        writeFile workspace "apps/peer/PROJECT" """
project peer { @shell {} }
target build { @shell echo { args = "build peer" } }
"""

        let options =
            { baseOptions workspace (Set [ "build" ]) with
                ConfigOptions.Options.Projects = Some (Set [ "app" ]) }
        let stages = runPipeline options
        let appBuild = "workspace/path#apps/app:build"
        let peerBuild = "workspace/path#apps/peer:build"
        let pnpmDist = "workspace/path#tools/pnpm:dist"
        let pnpmPrepare = "workspace/path#tools/pnpm:prepare"

        stages.SourceGraph.Nodes.Keys |> Set.ofSeq
        |> should equal (Set [ appBuild; pnpmDist; pnpmPrepare ])
        stages.SourceGraph.Nodes[appBuild].Dependencies |> should contain pnpmDist
        stages.SourceGraph.Nodes[pnpmDist].Dependencies |> should contain pnpmPrepare
        stages.SourceGraph.Nodes.ContainsKey peerBuild |> should equal false)

[<Test>]
let ``Project phase nothing cancels workspace target phase inheritance`` () =
    withTempWorkspace (fun workspace ->
        writeFile workspace "WORKSPACE" """
workspace {}
phase application {}
target build { phase = phase.application }
"""
        writeFile workspace "apps/inherit/PROJECT" """
project inherit { @shell {} }
target build { @shell echo { args = terrabuild.phase } }
"""
        writeFile workspace "apps/optout/PROJECT" """
project optout { @shell {} }
target build {
  phase = nothing
  @shell echo { args = terrabuild.phase }
}
"""

        let stages = runPipeline (baseOptions workspace (Set [ "build" ]))
        stages.FullGraph.Nodes["workspace/path#apps/inherit:build"].Phase |> should equal (Some "application")
        stages.FullGraph.Nodes["workspace/path#apps/optout:build"].Phase |> should equal None
        stages.ResolvedGraph.Nodes["workspace/path#apps/inherit:build"].Operations.Head.Arguments |> should equal "application"
        stages.ResolvedGraph.Nodes["workspace/path#apps/optout:build"].Operations.Head.Arguments |> should equal "")

[<Test>]
let ``Phase assignment does not affect target hash`` () =
    withTempWorkspace (fun workspace ->
        let project = """
project app { @shell {} }
target build { @shell echo { args = "build" } }
"""
        writeFile workspace "apps/app/PROJECT" project
        writeFile workspace "tools/pnpm/PROJECT" """
project pnpm { @shell {} }
target dist {
  phase = phase.toolchains
  @shell echo { args = "dist" }
}
"""
        writeFile workspace "WORKSPACE" """
workspace {}
phase toolchains {}
phase application { depends_on = [ phase.toolchains ] }
target dist {}
target build { phase = phase.application }
"""
        let phased = runPipeline (baseOptions workspace (Set [ "build" ]))
        let nodeId = "workspace/path#apps/app:build"

        writeFile workspace "WORKSPACE" """
workspace {}
phase toolchains {}
phase application { depends_on = [ phase.toolchains ] }
target dist {}
target build {}
"""
        let unphased = runPipeline (baseOptions workspace (Set [ "build" ]))
        phased.ResolvedGraph.Nodes[nodeId].TargetHash |> should equal unphased.ResolvedGraph.Nodes[nodeId].TargetHash)

[<Test>]
let ``Configuration rejects undefined and cyclic phases`` () =
    withTempWorkspace (fun workspace ->
        writeFile workspace "WORKSPACE" """
workspace {}
phase application { depends_on = [ phase.missing ] }
target build {}
"""
        let options = baseOptions workspace (Set [ "build" ])
        assertKnownErrorContains "depends on undefined phase 'missing'" (fun () -> Configuration.read options |> ignore)

        writeFile workspace "WORKSPACE" """
workspace {}
target build { phase = phase.missing }
"""
        assertKnownErrorContains "Phase 'missing' is not defined in WORKSPACE" (fun () -> Configuration.read options |> ignore)

        writeFile workspace "WORKSPACE" """
workspace {}
phase first { depends_on = [ phase.second ] }
phase second { depends_on = [ phase.first ] }
target build {}
"""
        assertKnownErrorContains "Circular phase dependency detected" (fun () -> Configuration.read options |> ignore))

[<Test>]
let ``Phase lowering rejects cycles combined with ordinary dependencies`` () =
    withTempWorkspace (fun workspace ->
        writeFile workspace "WORKSPACE" """
workspace {}
phase toolchains {}
phase application { depends_on = [ phase.toolchains ] }
target build {}
target dist { depends_on = [ target.build ] }
"""
        writeFile workspace "app/PROJECT" """
project app { @shell {} }
target build {
  phase = phase.application
  @shell echo { args = "build" }
}
target dist {
  phase = phase.toolchains
  @shell echo { args = "dist" }
}
"""

        let options, config = Configuration.read (baseOptions workspace (Set [ "build" ]))
        let nodeGraph = GraphPipeline.Node.build options config
        assertKnownErrorContains "Circular target dependency detected after applying phases" (fun () -> GraphPipeline.Phase.build nodeGraph |> ignore))
