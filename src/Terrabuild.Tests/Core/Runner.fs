module Terrabuild.Tests.Core.Runner
open System
open System.IO
open System.Collections.Generic
open System.Runtime.InteropServices
open FsUnit
open NUnit.Framework
open Contracts

let private buildNode id projectDir target action operations =
    { GraphDef.Node.Id = id
      GraphDef.Node.ProjectId = id
      GraphDef.Node.ProjectName = None
      GraphDef.Node.ProjectDir = projectDir
      GraphDef.Node.Target = target
      GraphDef.Node.Phase = None
      GraphDef.Node.Locks = Set.empty
      GraphDef.Node.Dependencies = Set.empty
      GraphDef.Node.PhaseDependencies = Set.empty
      GraphDef.Node.Outputs = Set.empty
      GraphDef.Node.ProjectHash = $"project-{id}"
      GraphDef.Node.TargetHash = $"target-{id}"
      GraphDef.Node.ClusterHash = Some "cluster"
      GraphDef.Node.Operations = operations
      GraphDef.Node.EvaluationInputs = []
      GraphDef.Node.EnvironmentSensitive = None
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
      ConfigOptions.Options.DryRun = false
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
      ConfigOptions.Options.GroupId = None
      ConfigOptions.Options.Label = None
      ConfigOptions.Options.Types = None
      ConfigOptions.Options.Labels = None
      ConfigOptions.Options.Projects = None
      ConfigOptions.Options.Variables = Map.empty
      ConfigOptions.Options.Engine = ConfigOptions.Engine.Host
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

[<Test>]
let ``operation argument fingerprints are stable across machine roots`` () =
    let unixOptions =
        { baseOptions "/Users/alice/src/terrabuild" with
            HomeDir = "/Users/alice/.terrabuild/home"
            TmpDir = "/Users/alice/.terrabuild/tmp"
            SharedDir = "/Users/alice/.terrabuild/shared" }
    let ciOptions =
        { baseOptions "/home/runner/work/terrabuild/terrabuild" with
            HomeDir = "/home/runner/.terrabuild/home"
            TmpDir = "/home/runner/.terrabuild/tmp"
            SharedDir = "/home/runner/.terrabuild/shared" }
    let unixArguments =
        "run -v /Users/alice/src/terrabuild:/terrabuild -v /Users/alice/.terrabuild/home:/terrabuild-home -v /Users/alice/.terrabuild/tmp:/terrabuild-tmp"
    let ciArguments =
        "run --user 1001:1001 -v /home/runner/work/terrabuild/terrabuild:/terrabuild -v /home/runner/.terrabuild/home:/terrabuild-home -v /home/runner/.terrabuild/tmp:/terrabuild-tmp"

    Diagnostics.normalizeOperationArguments unixOptions unixArguments
    |> should equal (Diagnostics.normalizeOperationArguments ciOptions ciArguments)

[<Test>]
let ``operation argument fingerprints ignore unique container instance suffixes`` () =
    let options = baseOptions "/workspace"
    let first = "run --rm --name terrabuild-workspace-path-app-build-0123456789ab image command"
    let second = "run --rm --name terrabuild-workspace-path-app-build-fedcba987654 image command"

    Diagnostics.normalizeOperationArguments options first
    |> should equal (Diagnostics.normalizeOperationArguments options second)

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

[<Test>]
let ``named target locks serialize competing leases and survive release`` () =
    withTempWorkspace (fun root ->
        let profile = Path.Combine(root, "profile")
        let first = TargetLock.acquireAt profile (Set [ "nuget-tools" ])
        use attempted = new System.Threading.ManualResetEventSlim(false)

        let second =
            System.Threading.Tasks.Task.Run(fun () ->
                attempted.Set()
                use _lease = TargetLock.acquireAt profile (Set [ "nuget-tools" ])
                ())

        attempted.Wait(TimeSpan.FromSeconds(2.0)) |> should equal true
        second.Wait(TimeSpan.FromSeconds(3.0)) |> should equal false
        first.Dispose()
        second.Wait(TimeSpan.FromSeconds(2.0)) |> should equal true
        File.Exists(TargetLock.lockFilePath profile "nuget-tools") |> should equal true)

[<Test>]
let ``clearing target locks removes idle sentinels without disrupting active leases`` () =
    withTempWorkspace (fun root ->
        let profile = Path.Combine(root, "profile")
        let active = TargetLock.acquireAt profile (Set [ "active" ])
        use idle = TargetLock.acquireAt profile (Set [ "idle" ])
        idle.Dispose()
        let restoreLock = Path.Combine(profile, "locks", "restores", "idle.lock")
        restoreLock |> FS.parentDirectory |> Option.iter IO.createDirectory
        File.WriteAllText(restoreLock, "")

        TargetLock.clearAt profile

        File.Exists(TargetLock.lockFilePath profile "active") |> should equal true
        File.Exists(TargetLock.lockFilePath profile "idle") |> should equal false
        File.Exists(restoreLock) |> should equal false

        active.Dispose()
        TargetLock.clearAt profile
        File.Exists(TargetLock.lockFilePath profile "active") |> should equal false)

let private withEnvironmentVariable name value action =
    let previous = Environment.GetEnvironmentVariable(name)
    Environment.SetEnvironmentVariable(name, value)
    try
        action ()
    finally
        Environment.SetEnvironmentVariable(name, previous)

let private writeExecutableScript (path: string) (content: string) =
    File.WriteAllText(path, content)

    if not (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) then
        File.SetUnixFileMode(path, UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute)

[<Test; CancelAfter(10000)>]
let ``captured processes drain large stdout and stderr streams concurrently`` () =
    if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) |> not then
        withTempWorkspace (fun workspace ->
            let script = Path.Combine(workspace, "large-output.sh")
            writeExecutableScript script """#!/bin/sh
i=0
while [ "$i" -lt 20000 ]; do
  printf 'stdout-012345678901234567890123456789\n'
  printf 'stderr-012345678901234567890123456789\n' >&2
  i=$((i + 1))
done
exit 7
"""

            match Exec.execCaptureOutput workspace script "" Map.empty with
            | Exec.Error (stderr, 7) -> stderr.Length |> should be (greaterThan 500000)
            | result -> Assert.Fail($"Expected captured exit code 7, got {result}"))

let private linuxRuntime =
    { Runner.HostRuntime.Platform = Environment.HostPlatform.Linux
      Runner.HostRuntime.UserId = Some 1000u
      Runner.HostRuntime.GroupId = Some 1001u }

let private macRuntime =
    { Runner.HostRuntime.Platform = Environment.HostPlatform.MacOS
      Runner.HostRuntime.UserId = Some 501u
      Runner.HostRuntime.GroupId = Some 20u }

let private buildOperation command arguments image =
    { GraphDef.ContaineredShellOperation.Image = image
      GraphDef.ContaineredShellOperation.Platform = Some "linux/amd64"
      GraphDef.ContaineredShellOperation.Cpus = Some 2
      GraphDef.ContaineredShellOperation.Variables = Set [ "TB_SAMPLE" ]
      GraphDef.ContaineredShellOperation.Envs = Map [ "FROM_ENV_MAP", "set-by-terrabuild" ]
      GraphDef.ContaineredShellOperation.MetaCommand = "test"
      GraphDef.ContaineredShellOperation.Command = command
      GraphDef.ContaineredShellOperation.Arguments = arguments
      GraphDef.ContaineredShellOperation.ErrorLevel = 0
      GraphDef.ContaineredShellOperation.Stdout = None }

type private FakeEntry(root: string, id: string, completed: ResizeArray<string>, disposed: ResizeArray<string>, onStoreOutputs: unit -> unit) =
    let entryRoot = Path.Combine(root, id.Replace("/", "_"))
    let logsDir = Path.Combine(entryRoot, "logs")
    let outputsDir = Path.Combine(entryRoot, "outputs")
    let mutable logIndex = 0

    do
        Directory.CreateDirectory(logsDir) |> ignore
        Directory.CreateDirectory(outputsDir) |> ignore

    interface Cache.IEntry with
        member _.Dispose() = disposed.Add(id)
        member _.NextLogFile() =
            logIndex <- logIndex + 1
            Path.Combine(logsDir, $"step{logIndex}.log")

        member _.StoreOutputs sourceDir entries =
            onStoreOutputs()
            match IO.copyFiles outputsDir sourceDir entries with
            | Some _ -> Cache.OutputState.Stored
            | None -> Cache.OutputState.Empty
        member _.StoreLogs entries =
            for entry in entries do
                File.Copy(entry, Path.Combine(logsDir, IO.getFilename entry), true)

        member _.Complete(_summary) =
            completed.Add(id)
            [ $"artifact-{id}" ]

type private FakeCache(root: string, ?onStoreOutputs: unit -> unit, ?onRestore: unit -> unit) =
    let completed = ResizeArray<string>()
    let disposed = ResizeArray<string>()
    let entries = Dictionary<string, FakeEntry>()
    let opened = ResizeArray<string>()
    let summaries = Dictionary<string, Cache.TargetSummary>()
    let restoreOutputs = Dictionary<string, string>()
    let onStoreOutputs = defaultArg onStoreOutputs ignore
    let onRestore = defaultArg onRestore ignore

    member _.Completed = completed |> Seq.toList
    member _.Disposed = disposed |> Seq.toList
    member _.Opened = opened |> Seq.toList
    member _.SetSummary(id, summary) = summaries[id] <- summary
    member _.SetRestoreOutputs(id, directory) = restoreOutputs[id] <- directory

    interface Cache.ICache with
        member _.TryGetSummaryOnly _useRemote id =
            match summaries.TryGetValue(id) with
            | true, summary -> Some (Cache.Origin.Local, summary)
            | _ -> None

        member _.CanRestore _useRemote _id _summary = true

        member _.TryGetSummary _useRemote id =
            match summaries.TryGetValue(id) with
            | true, summary -> Some summary
            | _ -> None

        member _.Restore _useRemote id outputs projectDirectory =
            onRestore()
            match summaries.TryGetValue(id) with
            | true, summary ->
                match restoreOutputs.TryGetValue(id) with
                | true, source ->
                    let files = IO.enumerateFiles source
                    let cached = files |> Seq.map (FS.relativePath source) |> Set.ofSeq
                    for current in (IO.createSnapshot outputs projectDirectory).TimestampedFiles.Keys do
                        if cached.Contains(FS.relativePath projectDirectory current) |> not then File.Delete(current)
                    IO.copyFiles projectDirectory source files |> ignore
                | _ -> ()
                Some summary
            | _ -> None

        member _.GetEntry _useRemote id =
            opened.Add(id)

            match entries.TryGetValue(id) with
            | true, entry -> entry :> Cache.IEntry
            | _ ->
                let entry = new FakeEntry(root, id, completed, disposed, onStoreOutputs)
                entries[id] <- entry
                entry :> Cache.IEntry

type private FakeApiClient(?failAddArtifact: bool) =
    let addCalls = ResizeArray<string * string option * string * string * string * string list * bool * DateTime * DateTime>()
    let useCalls = ResizeArray<string * string>()
    let graphUploads = ResizeArray<string * BuildGraphNode list>()
    let lifecycle = ResizeArray<string>()
    let completions = ResizeArray<bool>()
    let failAddArtifact = defaultArg failAddArtifact false

    member _.AddCalls = addCalls |> Seq.toList
    member _.UseCalls = useCalls |> Seq.toList
    member _.GraphUploads = graphUploads |> Seq.toList
    member _.Lifecycle = lifecycle |> Seq.toList
    member _.Completions = completions |> Seq.toList

    interface Contracts.IApiClient with
        member _.StartBuild() =
            lifecycle.Add("start")

        member _.UploadBuildGraph graphHash _environment nodes =
            lifecycle.Add("upload-graph")
            graphUploads.Add(graphHash, nodes)

        member _.CompleteBuild(success) =
            lifecycle.Add("complete")
            completions.Add(success)

        member _.GetCommitGraph _repository _commit _environment =
            { Contracts.CommitGraph.Repository = "acme/repo"
              Contracts.CommitGraph.Commit = "base"
              Contracts.CommitGraph.GraphHash = "graph"
              Contracts.CommitGraph.Nodes = [] }

        member _.GetArtifact(_path) (_operation) = Uri("https://example.invalid/artifact"), None

        member _.AddArtifact project projectName target projectHash targetHash files success startedAt endedAt =
            if failAddArtifact then
                failwith "artifact publication failed"
            else
                addCalls.Add(project, projectName, target, projectHash, targetHash, files, success, startedAt, endedAt)

        member _.UseArtifact projectHash targetHash =
            useCalls.Add(projectHash, targetHash)

[<Test>]
let ``buildCommands formats docker container requests through docker path on linux`` () =
    withTempWorkspace (fun workspace ->
        withEnvironmentVariable "TB_SAMPLE" "$TERRABUILD_HOME/cache" (fun () ->
            let operation = buildOperation "dotnet" "build App.csproj" (Some "mcr.microsoft.com/dotnet/sdk:8.0")
            let node = buildNode "node-docker" "src/App" "build" GraphDef.RunAction.Exec [ operation ]
            let options = { baseOptions workspace with Engine = ConfigOptions.Engine.Docker }

            let commands = Runner.buildCommandsForRuntime linuxRuntime node options "src/App" workspace workspace
            commands.Length |> should equal 1

            let metaCommand, workDir, cmd, args, image, errorLevel, envs, stdout = commands[0]
            let args = Exec.renderArguments args
            metaCommand |> should equal "test"
            workDir |> should equal workspace
            cmd |> should equal "docker"
            image |> should equal operation.Image
            errorLevel |> should equal 0
            envs |> should equal (Map [ "FROM_ENV_MAP", "set-by-terrabuild"; "TB_SAMPLE", "/terrabuild-home/cache" ])
            stdout |> should equal None
            args |> should contain "--entrypoint dotnet"
            args |> should contain "--platform=linux/amd64"
            args |> should contain "--cpus=2"
            args |> should contain "--user 1000:1001"
            args |> should not' (contain "-v /var/run/docker.sock:/var/run/docker.sock")
            args |> should contain $"-v {workspace}:/terrabuild-home"
            args |> should contain $"-v {workspace}:/terrabuild-tmp"
            args |> should contain "-e HOME=/terrabuild-home"
            args |> should contain "-e TERRABUILD_HOME=/terrabuild-home"
            args |> should contain "-e TMPDIR=/terrabuild-tmp"
            args |> should contain "-e TB_SAMPLE"
            args |> should not' (contain "/terrabuild-home/cache")
            args |> should contain "-e FROM_ENV_MAP"
            args |> should contain "mcr.microsoft.com/dotnet/sdk:8.0"
            args |> should contain "build App.csproj"))

[<Test>]
let ``buildCommands preserves spaced paths and uses unique readable container names`` () =
    withTempWorkspace (fun root ->
        let workspace = Path.Combine(root, "workspace with spaces")
        Directory.CreateDirectory(workspace) |> ignore
        let operation = buildOperation "dotnet" "build \"Project With Spaces.csproj\"" (Some "sdk:image")
        let node = buildNode "workspace/path#src/My App:build" "src/My App" "build" GraphDef.RunAction.Exec [ operation ]
        let options = { baseOptions workspace with Engine = ConfigOptions.Engine.Docker }

        let getArgs () =
            let _, _, _, arguments, _, _, _, _ =
                Runner.buildCommandsForRuntime linuxRuntime node options node.ProjectDir workspace workspace
                |> List.exactlyOne
            match arguments with
            | Exec.Arguments.List args -> args
            | _ -> Assert.Fail("Expected structured container arguments"); []

        let first = getArgs ()
        let second = getArgs ()
        first |> should contain $"{workspace}:/terrabuild"
        first |> should contain $"{workspace}:/terrabuild-home"
        first |> should contain "Project With Spaces.csproj"

        let containerName args =
            args
            |> List.windowed 2
            |> List.find (fun pair -> pair.Head = "--name")
            |> List.last
        (containerName first).StartsWith("terrabuild-workspace-path-src-my-app-build-") |> should equal true
        containerName first = containerName second |> should equal false)

[<Test>]
let ``buildCommands omits docker user mapping on macos`` () =
    withTempWorkspace (fun workspace ->
        let operation = buildOperation "dotnet" "restore App.csproj" (Some "mcr.microsoft.com/dotnet/sdk:8.0")
        let node = buildNode "node-docker-macos" "src/App" "build" GraphDef.RunAction.Exec [ operation ]
        let options = { baseOptions workspace with Engine = ConfigOptions.Engine.Docker }

        let commands = Runner.buildCommandsForRuntime macRuntime node options "src/App" workspace workspace
        commands.Length |> should equal 1

        let _, _, cmd, args, _, _, _, _ = commands[0]
        let args = Exec.renderArguments args
        cmd |> should equal "docker"
        args |> should not' (contain "--user 501:20"))

[<Test>]
let ``buildCommands formats podman container requests through podman path on linux`` () =
    withTempWorkspace (fun workspace ->
        let operation = buildOperation "dotnet" "restore App.csproj" (Some "mcr.microsoft.com/dotnet/sdk:8.0")
        let node = buildNode "node-podman" "src/App" "build" GraphDef.RunAction.Exec [ operation ]
        let options = { baseOptions workspace with Engine = ConfigOptions.Engine.Podman }

        let commands = Runner.buildCommandsForRuntime linuxRuntime node options "src/App" workspace workspace
        commands.Length |> should equal 1

        let _, workDir, cmd, args, _, _, _, _ = commands[0]
        let args = Exec.renderArguments args
        workDir |> should equal workspace
        cmd |> should equal "podman"
        args |> should contain "--entrypoint dotnet"
        args |> should contain "--userns=keep-id"
        args |> should contain "--security-opt label=disable"
        args |> should contain $"--mount type=bind,src={workspace},target=/terrabuild-home"
        args |> should contain $"--mount type=bind,src={workspace},target=/terrabuild-tmp"
        args |> should contain $"--mount type=bind,src={workspace},target=/terrabuild"
        args |> should contain "restore App.csproj")

[<Test>]
let ``buildCommands mounts docker socket only for docker client commands`` () =
    withTempWorkspace (fun workspace ->
        let operation = buildOperation "docker" "version" (Some "docker:27-cli")
        let node = buildNode "node-docker-socket" "src/App" "build" GraphDef.RunAction.Exec [ operation ]
        let options = { baseOptions workspace with Engine = ConfigOptions.Engine.Docker }

        let commands = Runner.buildCommandsForRuntime linuxRuntime node options "src/App" workspace workspace
        commands.Length |> should equal 1

        let _, _, _, args, _, _, _, _ = commands[0]
        let args = Exec.renderArguments args
        args |> should contain "-v /var/run/docker.sock:/var/run/docker.sock")

[<Test>]
let ``buildCommands uses explicit host path when engine is host even with image`` () =
    withTempWorkspace (fun workspace ->
        let operation = buildOperation "/usr/bin/env" "printenv" (Some "ignored:latest")
        let node = buildNode "node-host" "src/App" "build" GraphDef.RunAction.Exec [ operation ]

        let commands = Runner.buildCommands node (baseOptions workspace) "src/App" workspace workspace
        commands.Length |> should equal 1

        let _, workDir, cmd, args, image, _, _, _ = commands[0]
        let args = Exec.renderArguments args
        workDir |> should equal "src/App"
        cmd |> should equal "/usr/bin/env"
        args |> should equal "printenv"
        image |> should equal operation.Image)

[<Test>]
let ``buildCommands uses host path when operation has no image regardless of engine`` () =
    withTempWorkspace (fun workspace ->
        let operation = buildOperation "/usr/bin/true" "--flag" None
        let node = buildNode "node-no-image" "src/App" "build" GraphDef.RunAction.Exec [ operation ]
        let options = { baseOptions workspace with Engine = ConfigOptions.Engine.Docker }

        let commands = Runner.buildCommandsForRuntime linuxRuntime node options "src/App" workspace workspace
        commands.Length |> should equal 1

        let _, workDir, cmd, args, image, _, _, _ = commands[0]
        let args = Exec.renderArguments args
        workDir |> should equal "src/App"
        cmd |> should equal "/usr/bin/true"
        args |> should equal "--flag"
        image |> should equal None)

[<Test>]
let ``execCommands writes captured stdout without stderr`` () =
    withTempWorkspace (fun workspace ->
        let script = Path.Combine(workspace, "emit-output.sh")
        writeExecutableScript script "#!/bin/sh\nprintf 'first\\nsecond\\n'\nprintf 'warning\\n' >&2\n"

        let operation =
            { buildOperation script "" None with
                Stdout = Some "captured.txt" }
        let node = buildNode "node-capture" workspace "build" GraphDef.RunAction.Exec [ operation ]
        let cache = FakeCache(workspace)
        let entry = (cache :> Cache.ICache).GetEntry false (GraphDef.buildCacheKey node)

        let successful, exitCode, logs =
            Runner.execCommands node entry (baseOptions workspace) workspace workspace workspace

        successful |> should equal true
        exitCode |> should equal 0
        File.ReadAllText(Path.Combine(workspace, "captured.txt")) |> should equal $"first{Environment.NewLine}second{Environment.NewLine}"
        File.ReadAllText(logs.Head.Log) |> should contain "warning")

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
          GraphDef.Graph.Batches = Map [ batchNode.Id, Set [ memberA.Id; memberB.Id ] ]
          GraphDef.Graph.Phases = Map.empty }

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
          GraphDef.Graph.Batches = Map [ batchNode.Id, Set [ memberA.Id; memberB.Id ] ]
          GraphDef.Graph.Phases = Map.empty }

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
              GraphDef.ContaineredShellOperation.ErrorLevel = 0
              GraphDef.ContaineredShellOperation.Stdout = None }

        let execMember =
            { buildNode "member-exec" workspace "build" GraphDef.RunAction.Exec [] with
                Phase = Some "application" }
        let restoreMember = buildNode "member-restore" workspace "build" GraphDef.RunAction.Restore []
        let batchNode = buildNode "batch-build" "." "build" GraphDef.RunAction.Exec [ operation ]
        let graph =
            { GraphDef.Graph.Nodes =
                [ execMember.Id, execMember
                  restoreMember.Id, restoreMember
                  batchNode.Id, batchNode ] |> Map.ofList
              GraphDef.Graph.RootNodes = Set [ execMember.Id; restoreMember.Id ]
              GraphDef.Graph.Batches = Map [ batchNode.Id, Set [ execMember.Id; restoreMember.Id ] ]
              GraphDef.Graph.Phases = Map.empty }

        let cache = FakeCache(workspace)
        let api = FakeApiClient()
        let options = baseOptions workspace
        let summary = Runner.run options (cache :> Cache.ICache) (Some (api :> Contracts.IApiClient)) graph graph
        let report =
            Diagnostics.build {
                Diagnostics.Context.Options = options
                Configuration = None
                FullGraph = Some graph
                SelectedGraph = Some graph
                ResolvedGraph = Some graph
                FinalGraph = Some graph
                Cache = Some (cache :> Cache.ICache)
                Summary = Some summary
                Status = if summary.IsSuccess then "success" else "failure"
                Completeness = "complete"
                Error = None
            }

        cache.Completed |> should equal [ GraphDef.buildCacheKey execMember ]
        cache.Opened |> should equal [ GraphDef.buildCacheKey execMember; GraphDef.buildCacheKey batchNode ]
        api.AddCalls.Length |> should equal 1
        let (_, _, _, _, _, _, _, artifactStartedAt, artifactEndedAt) = api.AddCalls[0]
        api.AddCalls[0] |> should equal (
            execMember.ProjectDir,
            execMember.ProjectName,
            execMember.Target,
            execMember.ProjectHash,
            execMember.TargetHash,
            [ $"artifact-{GraphDef.buildCacheKey execMember}" ],
            expectedSuccess,
            artifactStartedAt,
            artifactEndedAt)
        artifactEndedAt >= artifactStartedAt |> should equal true
        api.UseCalls |> should equal [ (restoreMember.ProjectHash, restoreMember.TargetHash) ]
        api.Lifecycle |> should equal [ "start"; "upload-graph"; "complete" ]
        api.GraphUploads.Length |> should equal 1
        let (_, uploadedNodes) = api.GraphUploads[0]
        uploadedNodes |> List.map (fun node -> node.Id) |> Set.ofList |> should equal (Set [ execMember.Id; restoreMember.Id; batchNode.Id ])
        uploadedNodes |> List.find (fun node -> node.Id = execMember.Id) |> should equal {
            Contracts.BuildGraphNode.Id = execMember.Id
            Contracts.BuildGraphNode.ProjectId = execMember.ProjectId
            Contracts.BuildGraphNode.ProjectName = execMember.ProjectName
            Contracts.BuildGraphNode.ProjectDir = execMember.ProjectDir
            Contracts.BuildGraphNode.Target = execMember.Target
            Contracts.BuildGraphNode.Phase = execMember.Phase
            Contracts.BuildGraphNode.ProjectHash = execMember.ProjectHash
            Contracts.BuildGraphNode.TargetHash = execMember.TargetHash
            Contracts.BuildGraphNode.Dependencies = execMember.Dependencies |> Seq.sort |> List.ofSeq
            Contracts.BuildGraphNode.Artifacts = string execMember.Artifacts
            Contracts.BuildGraphNode.Build = string execMember.Build
            Contracts.BuildGraphNode.Batch = string execMember.Batch
            Contracts.BuildGraphNode.Action = string execMember.Action
            Contracts.BuildGraphNode.Required = execMember.Required
            Contracts.BuildGraphNode.IsBatchNode = false
        }

        match summary.Nodes[execMember.Id].Request with
        | Runner.TaskRequest.Exec -> ()
        | request -> Assert.Fail($"Expected exec request for exec member, got {request}")

        match summary.Nodes[restoreMember.Id].Request with
        | Runner.TaskRequest.Restore -> ()
        | request -> Assert.Fail($"Expected restore request for restored member, got {request}")

        summary.Nodes[execMember.Id].Status.IsSuccess |> should equal expectedSuccess
        summary.Nodes[restoreMember.Id].Status.IsSuccess |> should equal expectedSuccess
        summary.Nodes |> Map.containsKey batchNode.Id |> should equal false

        let restoreReport = report.Nodes |> List.find (fun node -> node.Id = restoreMember.Id)
        restoreReport.Outcome |> should equal (Some "restore")
        restoreReport.OutcomeReason |> should equal (Some "batch-cache-reuse")
        restoreReport.BatchId |> should equal (Some batchNode.Id)
        report.Batches |> List.map _.Id |> should contain batchNode.Id)

[<Test>]
let ``batch output staging completes while named target lock is held`` () =
    withTempWorkspace (fun workspace ->
        withEnvironmentVariable "HOME" workspace (fun () ->
            DiagnosticsTelemetry.reset true
            let projectDir = Path.Combine(workspace, "project")
            Directory.CreateDirectory(projectDir) |> ignore
            let output = Path.Combine(projectDir, "generated.txt")
            let lockName = "batch-finalization"
            let lockPath = TargetLock.lockFilePath (Path.Combine(workspace, ".terrabuild")) lockName
            let mutable observedLease = false

            let assertLeaseHeld () =
                try
                    use _stream = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
                    observedLease <- false
                with :? IOException ->
                    observedLease <- true

            let memberNode =
                { buildNode "member-exec" projectDir "build" GraphDef.RunAction.Exec [] with
                    Locks = Set [ lockName ]
                    Outputs = Set [ "generated.txt" ] }
            let batchNode =
                { buildNode "batch-build" "." "build" GraphDef.RunAction.Exec [ buildOperation "/usr/bin/touch" output None ] with
                    Locks = Set [ lockName ] }
            let graph =
                { GraphDef.Graph.Nodes = Map [ memberNode.Id, memberNode; batchNode.Id, batchNode ]
                  GraphDef.Graph.RootNodes = Set [ memberNode.Id ]
                  GraphDef.Graph.Batches = Map [ batchNode.Id, Set [ memberNode.Id ] ]
                  GraphDef.Graph.Phases = Map.empty }

            let cache = FakeCache(workspace, assertLeaseHeld)
            let summary = Runner.run (baseOptions workspace) (cache :> Cache.ICache) None graph graph

            summary.IsSuccess |> should equal true
            observedLease |> should equal true
            cache.Completed |> should equal [ GraphDef.buildCacheKey memberNode ]
            let batchEvents =
                DiagnosticsTelemetry.snapshot().TaskEvents
                |> List.filter (fun event -> event.TaskId = batchNode.Id)
                |> List.map _.Event
            batchEvents |> should contain "finalization-started"
            batchEvents |> should contain "finalization-ended"))

[<Test>]
let ``managed output restore completes while named target lock is held`` () =
    withTempWorkspace (fun workspace ->
        withEnvironmentVariable "HOME" workspace (fun () ->
            let projectDir = Path.Combine(workspace, "project")
            Directory.CreateDirectory(projectDir) |> ignore
            let lockName = "restore-finalization"
            let lockPath = TargetLock.lockFilePath (Path.Combine(workspace, ".terrabuild")) lockName
            let mutable observedLease = false

            let assertLeaseHeld () =
                try
                    use _stream = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
                    observedLease <- false
                with :? IOException ->
                    observedLease <- true

            let node =
                { buildNode "restore-node" projectDir "build" GraphDef.RunAction.Restore [] with
                    Locks = Set [ lockName ]
                    Outputs = Set [ "generated/**" ]
                    Artifacts = GraphDef.ArtifactMode.Managed }
            let graph =
                { GraphDef.Graph.Nodes = Map [ node.Id, node ]
                  GraphDef.Graph.RootNodes = Set [ node.Id ]
                  GraphDef.Graph.Batches = Map.empty
                  GraphDef.Graph.Phases = Map.empty }
            let cache = FakeCache(workspace, onRestore = assertLeaseHeld)
            let cachedSummary =
                { Cache.TargetSummary.Project = node.ProjectDir
                  Cache.TargetSummary.Target = node.Target
                  Cache.TargetSummary.Operations = []
                  Cache.TargetSummary.Outputs = Cache.OutputState.Stored
                  Cache.TargetSummary.IsSuccessful = true
                  Cache.TargetSummary.StartedAt = DateTime.UtcNow.AddMinutes(-1.0)
                  Cache.TargetSummary.EndedAt = DateTime.UtcNow
                  Cache.TargetSummary.Duration = TimeSpan.FromSeconds(1.0)
                  Cache.TargetSummary.Cache = node.Artifacts }
            cache.SetSummary(GraphDef.buildCacheKey node, cachedSummary)

            let summary = Runner.run (baseOptions workspace) (cache :> Cache.ICache) None graph graph

            summary.IsSuccess |> should equal true
            observedLease |> should equal true))

[<Test>]
let ``missing cached outputs fall back to target execution`` () =
    withTempWorkspace (fun workspace ->
        DiagnosticsTelemetry.reset true
        let marker = Path.Combine(workspace, "rebuilt.txt")
        let node =
            { buildNode "restore-miss" workspace "build" GraphDef.RunAction.Restore [ buildOperation "/usr/bin/touch" marker None ] with
                Outputs = Set [ "rebuilt.txt" ]
                Artifacts = GraphDef.ArtifactMode.Managed }
        let graph =
            { GraphDef.Graph.Nodes = Map [ node.Id, node ]
              GraphDef.Graph.RootNodes = Set [ node.Id ]
              GraphDef.Graph.Batches = Map.empty
              GraphDef.Graph.Phases = Map.empty }
        let cache = FakeCache(workspace)

        let options = baseOptions workspace
        let summary = Runner.run options (cache :> Cache.ICache) None graph graph
        let report =
            Diagnostics.build {
                Diagnostics.Context.Options = options
                Configuration = None
                FullGraph = Some graph
                SelectedGraph = Some graph
                ResolvedGraph = Some graph
                FinalGraph = Some graph
                Cache = Some (cache :> Cache.ICache)
                Summary = Some summary
                Status = "success"
                Completeness = "complete"
                Error = None
            }

        File.Exists(marker) |> should equal true
        summary.IsSuccess |> should equal true
        summary.Nodes[node.Id].Request |> should equal Runner.TaskRequest.Exec
        let nodeReport = report.Nodes |> List.exactlyOne
        nodeReport.Outcome |> should equal (Some "execute")
        nodeReport.OutcomeReason |> should equal (Some "restore-missed")
        (report.Executions |> List.exactlyOne).Kind |> should equal "execution")

[<Test>]
let ``named target lock waits are reported separately from execution`` () =
    withTempWorkspace (fun workspace ->
        withEnvironmentVariable "HOME" workspace (fun () ->
            DiagnosticsTelemetry.reset true
            try
                let lockName = "reported-lock"
                let profile = Path.Combine(workspace, ".terrabuild")
                let heldLease = TargetLock.acquireAt profile (Set [ lockName ])
                let release =
                    System.Threading.Tasks.Task.Run(fun () ->
                        System.Threading.Thread.Sleep(200)
                        heldLease.Dispose())

                let node =
                    { buildNode "locked-node" workspace "build" GraphDef.RunAction.Exec [ buildOperation "/usr/bin/true" "" None ] with
                        Locks = Set [ lockName ] }
                let graph =
                    { GraphDef.Graph.Nodes = Map [ node.Id, node ]
                      GraphDef.Graph.RootNodes = Set [ node.Id ]
                      GraphDef.Graph.Batches = Map.empty
                      GraphDef.Graph.Phases = Map.empty }
                let cache = FakeCache(workspace)
                let options = { baseOptions workspace with Debug = true }
                let summary = Runner.run options (cache :> Cache.ICache) None graph graph
                release.Wait()

                let report =
                    Diagnostics.build {
                        Diagnostics.Context.Options = options
                        Configuration = None
                        FullGraph = Some graph
                        SelectedGraph = Some graph
                        ResolvedGraph = Some graph
                        FinalGraph = Some graph
                        Cache = Some (cache :> Cache.ICache)
                        Summary = Some summary
                        Status = "success"
                        Completeness = "complete"
                        Error = None
                    }

                let execution = report.Executions |> List.exactlyOne
                execution.Events |> List.map _.Event
                |> should contain "lock-wait-started"
                execution.Events |> List.map _.Event
                |> should contain "lock-acquired"
                execution.LockWaitMs |> Option.defaultValue 0.0 |> should be (greaterThanOrEqualTo 150.0)
                execution.DurationMs |> Option.defaultValue Double.MaxValue |> should be (lessThan 150.0)
                report.Nodes |> List.exactlyOne |> _.Locks |> should equal [ lockName ]
            finally
                DiagnosticsTelemetry.reset false))

[<Test>]
let ``run includes repository in uploaded graph hash`` () =
    withTempWorkspace (fun workspace ->
        let operation =
            { GraphDef.ContaineredShellOperation.Image = None
              GraphDef.ContaineredShellOperation.Platform = None
              GraphDef.ContaineredShellOperation.Cpus = None
              GraphDef.ContaineredShellOperation.Variables = Set.empty
              GraphDef.ContaineredShellOperation.Envs = Map.empty
              GraphDef.ContaineredShellOperation.MetaCommand = "test"
              GraphDef.ContaineredShellOperation.Command = "/usr/bin/true"
              GraphDef.ContaineredShellOperation.Arguments = ""
              GraphDef.ContaineredShellOperation.ErrorLevel = 0
              GraphDef.ContaineredShellOperation.Stdout = None }

        let memberNode = buildNode "member-exec" workspace "build" GraphDef.RunAction.Exec []
        let batchNode = buildNode "batch-build" "." "build" GraphDef.RunAction.Exec [ operation ]
        let graph =
            { GraphDef.Graph.Nodes =
                [ memberNode.Id, memberNode
                  batchNode.Id, batchNode ] |> Map.ofList
              GraphDef.Graph.RootNodes = Set [ memberNode.Id ]
              GraphDef.Graph.Batches = Map [ batchNode.Id, Set [ memberNode.Id ] ]
              GraphDef.Graph.Phases = Map.empty }

        let runForRepository repository =
            let cache = FakeCache(workspace)
            let api = FakeApiClient()
            let options =
                { baseOptions workspace with
                    ConfigOptions.Options.Repository = repository }
            Runner.run options (cache :> Cache.ICache) (Some (api :> Contracts.IApiClient)) graph graph |> ignore
            let (graphHash, _) = api.GraphUploads |> List.exactlyOne
            graphHash

        let firstHash = runForRepository "acme/repo-a"
        let secondHash = runForRepository "acme/repo-b"

        firstHash = secondHash |> should equal false)

[<Test>]
let ``run finalizes Insights as failed when artifact publication throws`` () =
    withTempWorkspace (fun workspace ->
        let node =
            buildNode
                "failed-publication"
                workspace
                "build"
                GraphDef.RunAction.Exec
                [ buildOperation "/usr/bin/true" "" None ]
        let graph =
            { GraphDef.Graph.Nodes = Map [ node.Id, node ]
              GraphDef.Graph.RootNodes = Set [ node.Id ]
              GraphDef.Graph.Batches = Map.empty
              GraphDef.Graph.Phases = Map.empty }
        let cache = FakeCache(workspace)
        let api = FakeApiClient(failAddArtifact = true)

        let failure =
            Assert.Throws<Runner.RunFailure>(Action(fun () ->
            Runner.run
                (baseOptions workspace)
                (cache :> Cache.ICache)
                (Some (api :> Contracts.IApiClient))
                graph
                graph
            |> ignore))
            |> Option.ofObj
            |> Option.get

        failure.Summary.IsSuccess |> should equal true
        failure.Summary.Nodes[node.Id].Request |> should equal Runner.TaskRequest.Exec
        failure.Summary.Nodes[node.Id].Status.IsSuccess |> should equal true

        api.Lifecycle |> should equal [ "start"; "upload-graph"; "complete" ]
        api.Completions |> should equal [ false ])

[<Test>]
let ``run disposes cache staging when output preparation throws`` () =
    withTempWorkspace (fun workspace ->
        let output = Path.Combine(workspace, "generated.txt")
        let node =
            { buildNode "failed-staging" workspace "build" GraphDef.RunAction.Exec [ buildOperation "/usr/bin/touch" output None ] with
                Outputs = Set [ "generated.txt" ] }
        let graph =
            { GraphDef.Graph.Nodes = Map [ node.Id, node ]
              GraphDef.Graph.RootNodes = Set [ node.Id ]
              GraphDef.Graph.Batches = Map.empty
              GraphDef.Graph.Phases = Map.empty }
        let cache = FakeCache(workspace, onStoreOutputs = fun () -> failwith "staging failed")

        Assert.Throws<Runner.RunFailure>(Action(fun () ->
            Runner.run (baseOptions workspace) (cache :> Cache.ICache) None graph graph |> ignore))
        |> ignore

        cache.Disposed |> should contain (GraphDef.buildCacheKey node))

[<Test>]
let ``run disposes every batch entry when member preparation throws`` () =
    withTempWorkspace (fun workspace ->
        let output = Path.Combine(workspace, "generated.txt")
        let first =
            { buildNode "first-member" workspace "build" GraphDef.RunAction.Exec [] with
                Outputs = Set [ "generated.txt" ] }
        let second =
            { buildNode "second-member" workspace "build" GraphDef.RunAction.Exec [] with
                Outputs = Set [ "generated.txt" ] }
        let batch = buildNode "batch-build" workspace "build" GraphDef.RunAction.Exec [ buildOperation "/usr/bin/touch" output None ]
        let graph =
            { GraphDef.Graph.Nodes = Map [ first.Id, first; second.Id, second; batch.Id, batch ]
              GraphDef.Graph.RootNodes = Set [ first.Id; second.Id ]
              GraphDef.Graph.Batches = Map [ batch.Id, Set [ first.Id; second.Id ] ]
              GraphDef.Graph.Phases = Map.empty }
        let cache = FakeCache(workspace, onStoreOutputs = fun () -> failwith "staging failed")

        Assert.Throws<Runner.RunFailure>(Action(fun () ->
            Runner.run (baseOptions workspace) (cache :> Cache.ICache) None graph graph |> ignore))
        |> ignore

        cache.Disposed |> Set.ofList
        |> should equal (Set [ GraphDef.buildCacheKey first; GraphDef.buildCacheKey second; GraphDef.buildCacheKey batch ]))

[<Test>]
let ``run normalizes equivalent repository identities in uploaded graph hash`` () =
    withTempWorkspace (fun workspace ->
        let operation =
            { GraphDef.ContaineredShellOperation.Image = None
              GraphDef.ContaineredShellOperation.Platform = None
              GraphDef.ContaineredShellOperation.Cpus = None
              GraphDef.ContaineredShellOperation.Variables = Set.empty
              GraphDef.ContaineredShellOperation.Envs = Map.empty
              GraphDef.ContaineredShellOperation.MetaCommand = "test"
              GraphDef.ContaineredShellOperation.Command = "/usr/bin/true"
              GraphDef.ContaineredShellOperation.Arguments = ""
              GraphDef.ContaineredShellOperation.ErrorLevel = 0
              GraphDef.ContaineredShellOperation.Stdout = None }

        let memberNode = buildNode "member-exec" workspace "build" GraphDef.RunAction.Exec []
        let batchNode = buildNode "batch-build" "." "build" GraphDef.RunAction.Exec [ operation ]
        let graph =
            { GraphDef.Graph.Nodes =
                [ memberNode.Id, memberNode
                  batchNode.Id, batchNode ] |> Map.ofList
              GraphDef.Graph.RootNodes = Set [ memberNode.Id ]
              GraphDef.Graph.Batches = Map [ batchNode.Id, Set [ memberNode.Id ] ]
              GraphDef.Graph.Phases = Map.empty }

        let runForRepository repository =
            let cache = FakeCache(workspace)
            let api = FakeApiClient()
            let options =
                { baseOptions workspace with
                    ConfigOptions.Options.Repository = repository }
            Runner.run options (cache :> Cache.ICache) (Some (api :> Contracts.IApiClient)) graph graph |> ignore
            let (graphHash, _) = api.GraphUploads |> List.exactlyOne
            graphHash

        let firstHash = runForRepository "git@github.com:acme/repo.git"
        let secondHash = runForRepository "acme/repo"

        firstHash |> should equal secondHash)

[<Test>]
let ``run restores the exact cached output set for lazy dependencies`` () =
    withTempWorkspace (fun workspace ->
        let genProjectDir = Path.Combine(workspace, "gen")
        let buildProjectDir = Path.Combine(workspace, "build")
        Directory.CreateDirectory(genProjectDir) |> ignore
        Directory.CreateDirectory(buildProjectDir) |> ignore

        let generatedDir = Path.Combine(genProjectDir, "generated")
        Directory.CreateDirectory(generatedDir) |> ignore
        File.WriteAllText(Path.Combine(generatedDir, "cached.txt"), "old-content")
        File.WriteAllText(Path.Combine(generatedDir, "stale.txt"), "stale-content")
        File.WriteAllText(Path.Combine(genProjectDir, "unrelated.txt"), "unrelated-content")

        let genNode =
            { buildNode "gen" genProjectDir "gen" GraphDef.RunAction.Restore []
                with Build = GraphDef.BuildMode.Lazy
                     Outputs = Set [ "generated/**" ] }

        let operation =
            { GraphDef.ContaineredShellOperation.Image = None
              GraphDef.ContaineredShellOperation.Platform = None
              GraphDef.ContaineredShellOperation.Cpus = None
              GraphDef.ContaineredShellOperation.Variables = Set.empty
              GraphDef.ContaineredShellOperation.Envs = Map.empty
              GraphDef.ContaineredShellOperation.MetaCommand = "test"
              GraphDef.ContaineredShellOperation.Command = "/usr/bin/true"
              GraphDef.ContaineredShellOperation.Arguments = ""
              GraphDef.ContaineredShellOperation.ErrorLevel = 0
              GraphDef.ContaineredShellOperation.Stdout = None }

        let buildNode =
            { buildNode "build" buildProjectDir "build" GraphDef.RunAction.Exec [ operation ]
                with Dependencies = Set [ genNode.Id ]
                     Outputs = Set [ "compiled.txt" ] }

        let graph =
            { GraphDef.Graph.Nodes =
                [ genNode.Id, genNode
                  buildNode.Id, buildNode ] |> Map.ofList
              GraphDef.Graph.RootNodes = Set [ buildNode.Id ]
              GraphDef.Graph.Batches = Map.empty
              GraphDef.Graph.Phases = Map.empty }

        let cache = FakeCache(workspace)
        let api = FakeApiClient()

        let makeSummary (node: GraphDef.Node) (files: (string * string) list) =
            let cacheOutputs = Path.Combine(workspace, $"cache-{node.Id}")
            Directory.CreateDirectory(cacheOutputs) |> ignore
            for filename, content in files do
                let output = Path.Combine(cacheOutputs, filename)
                Path.GetDirectoryName(output)
                |> Option.ofObj
                |> Option.iter (fun directory -> Directory.CreateDirectory(directory) |> ignore)
                File.WriteAllText(output, content)
            ({ Cache.TargetSummary.Project = node.ProjectDir
               Cache.TargetSummary.Target = node.Target
               Cache.TargetSummary.Operations = []
               Cache.TargetSummary.Outputs = Cache.OutputState.Stored
               Cache.TargetSummary.IsSuccessful = true
               Cache.TargetSummary.StartedAt = DateTime.UtcNow.AddMinutes(-1.0)
               Cache.TargetSummary.EndedAt = DateTime.UtcNow
               Cache.TargetSummary.Duration = TimeSpan.FromSeconds(1.0)
               Cache.TargetSummary.Cache = node.Artifacts }, cacheOutputs)

        let cacheKey = GraphDef.buildCacheKey genNode
        let cachedSummary, cachedOutputs =
            makeSummary genNode [ "generated/cached.txt", "cached-content"; "generated/new.txt", "new-content" ]
        cache.SetSummary(cacheKey, cachedSummary)
        cache.SetRestoreOutputs(cacheKey, cachedOutputs)
        let summary = Runner.run (baseOptions workspace) (cache :> Cache.ICache) (Some (api :> Contracts.IApiClient)) graph graph

        File.ReadAllText(Path.Combine(generatedDir, "cached.txt")) |> should equal "cached-content"
        File.ReadAllText(Path.Combine(generatedDir, "new.txt")) |> should equal "new-content"
        File.Exists(Path.Combine(generatedDir, "stale.txt")) |> should equal false
        File.ReadAllText(Path.Combine(genProjectDir, "unrelated.txt")) |> should equal "unrelated-content"
        cache.Completed |> should equal [ GraphDef.buildCacheKey buildNode ]
        api.AddCalls.Length |> should equal 1
        let (_, _, _, _, _, _, _, artifactStartedAt, artifactEndedAt) = api.AddCalls[0]
        api.AddCalls[0] |> should equal (
            buildNode.ProjectDir,
            buildNode.ProjectName,
            buildNode.Target,
            buildNode.ProjectHash,
            buildNode.TargetHash,
            [ $"artifact-{GraphDef.buildCacheKey buildNode}" ],
            true,
            artifactStartedAt,
            artifactEndedAt)
        artifactEndedAt >= artifactStartedAt |> should equal true
        api.UseCalls |> should equal [ (genNode.ProjectHash, genNode.TargetHash) ]

        summary.Nodes[genNode.Id].Request |> should equal Runner.TaskRequest.Restore
        summary.Nodes[buildNode.Id].Request |> should equal Runner.TaskRequest.Exec
        summary.Nodes[genNode.Id].Status.IsSuccess |> should equal true
        summary.Nodes[buildNode.Id].Status.IsSuccess |> should equal true)

[<Test>]
let ``runner waits for every prerequisite phase dependency`` () =
    withTempWorkspace (fun workspace ->
        let marker = Path.Combine(workspace, "toolchain-ready")
        let tool =
            { buildNode "tool" workspace "dist" GraphDef.RunAction.Exec [ buildOperation "/usr/bin/touch" marker None ] with
                Phase = Some "toolchains" }
        let app =
            { buildNode "app" workspace "build" GraphDef.RunAction.Exec [ buildOperation "/usr/bin/stat" marker None ] with
                Phase = Some "application"
                Dependencies = Set [ tool.Id ] }
        let graph =
            { GraphDef.Graph.Nodes = Map [ tool.Id, tool; app.Id, app ]
              GraphDef.Graph.RootNodes = Set [ app.Id ]
              GraphDef.Graph.Batches = Map.empty
              GraphDef.Graph.Phases = Map [ "toolchains", Set.empty; "application", Set [ "toolchains" ] ] }

        let summary = Runner.run (baseOptions workspace) (FakeCache(workspace) :> Cache.ICache) None graph graph

        File.Exists marker |> should equal true
        summary.IsSuccess |> should equal true)

[<Test>]
let ``runner reports cached failures as summaries rather than restores`` () =
    withTempWorkspace (fun workspace ->
        let node = buildNode "cached-failure" workspace "build" GraphDef.RunAction.Summary []
        let graph =
            { GraphDef.Graph.Nodes = Map [ node.Id, node ]
              GraphDef.Graph.RootNodes = Set [ node.Id ]
              GraphDef.Graph.Batches = Map.empty
              GraphDef.Graph.Phases = Map.empty }
        let cachedFailure =
            { Cache.TargetSummary.Project = node.ProjectDir
              Cache.TargetSummary.Target = node.Target
              Cache.TargetSummary.Operations = []
              Cache.TargetSummary.Outputs = Cache.OutputState.Empty
              Cache.TargetSummary.IsSuccessful = false
              Cache.TargetSummary.StartedAt = DateTime.UtcNow.AddMinutes(-1.0)
              Cache.TargetSummary.EndedAt = DateTime.UtcNow
              Cache.TargetSummary.Duration = TimeSpan.FromSeconds(1.0)
              Cache.TargetSummary.Cache = node.Artifacts }
        let cache = FakeCache(workspace)
        cache.SetSummary(GraphDef.buildCacheKey node, cachedFailure)

        let summary = Runner.run (baseOptions workspace) (cache :> Cache.ICache) None graph graph

        summary.Nodes[node.Id].Request |> should equal Runner.TaskRequest.Summary
        summary.Nodes[node.Id].Status.IsSuccess |> should equal false)

[<Test>]
let ``runner does not execute a downstream phase after prerequisite failure`` () =
    withTempWorkspace (fun workspace ->
        let downstreamMarker = Path.Combine(workspace, "application-ran")
        let tool =
            { buildNode "tool" workspace "dist" GraphDef.RunAction.Exec [ buildOperation "/usr/bin/false" "" None ] with
                Phase = Some "toolchains" }
        let app =
            { buildNode "app" workspace "build" GraphDef.RunAction.Exec [ buildOperation "/usr/bin/touch" downstreamMarker None ] with
                Phase = Some "application"
                Dependencies = Set [ tool.Id ] }
        let graph =
            { GraphDef.Graph.Nodes = Map [ tool.Id, tool; app.Id, app ]
              GraphDef.Graph.RootNodes = Set [ app.Id ]
              GraphDef.Graph.Batches = Map.empty
              GraphDef.Graph.Phases = Map [ "toolchains", Set.empty; "application", Set [ "toolchains" ] ] }

        let summary = Runner.run (baseOptions workspace) (FakeCache(workspace) :> Cache.ICache) None graph graph

        File.Exists downstreamMarker |> should equal false
        summary.IsSuccess |> should equal false
        match summary.Nodes[app.Id].Status with
        | Runner.TaskStatus.Blocked (_, dependencies) -> dependencies |> should equal [ tool.Id ]
        | status -> failwith $"Expected the downstream task to be blocked, got {status}"
        summary.Nodes[app.Id].Request |> should equal Runner.TaskRequest.Exec)
