module Runner
open System
open System.IO
open System.Collections.Generic
open System.Runtime.InteropServices
open Collections
open Serilog
open Terrabuild.PubSub
open Environment
open Errors
open Microsoft.Extensions.FileSystemGlobbing

[<RequireQualifiedAccess>]
type TaskRequest =
    | Exec
    | Restore

[<RequireQualifiedAccess>]
type TaskStatus =
    | Success of completionDate:DateTime
    | Failure of completionDate:DateTime * message:string

[<RequireQualifiedAccess>]
type NodeInfo = {
    Request: TaskRequest
    Status: TaskStatus
    Project: string
    Target: string
    ProjectHash: string
    TargetHash: string
}

[<RequireQualifiedAccess>]
type Summary = {
    Commit: string
    BranchOrTag: string
    StartedAt: DateTime
    EndedAt: DateTime
    IsSuccess: bool
    Targets: string set
    Nodes: Map<string, NodeInfo>
}

type private BuiltCommand = string * string * string * string * string option * int * Map<string, string>

type internal HostRuntime = {
    Platform: Environment.HostPlatform
    UserId: uint32 option
    GroupId: uint32 option
}

type private ContainerEnginePolicy = {
    EngineCommand: string
    ExtraArgs: string list
    MountArgs: string list
}

[<RequireQualifiedAccess>]
type private EngineRequestPath =
    | Docker
    | Podman
    | Host

let private resolveEngineRequestPath (engine: string option) =
    match engine with
    | Some "docker" -> EngineRequestPath.Docker
    | Some "podman" -> EngineRequestPath.Podman
    | _ -> EngineRequestPath.Host

module private Native =
    module Posix =
        [<DllImport("libc", SetLastError = true)>]
        extern uint32 getuid()

        [<DllImport("libc", SetLastError = true)>]
        extern uint32 getgid()

let private containerHome = "/terrabuild-home"
let private containerTmp = "/terrabuild-tmp"

let private detectHostRuntime () =
    let platform = detectHostPlatform ()

    let userId, groupId =
        match platform with
        | Environment.HostPlatform.Linux
        | Environment.HostPlatform.MacOS -> Some (Native.Posix.getuid()), Some (Native.Posix.getgid())
        | _ -> None, None

    { Platform = platform
      UserId = userId
      GroupId = groupId }

let private formatPlatform (operation: GraphDef.ContaineredShellOperation) =
    operation.Platform |> Option.map (fun platform -> $"--platform={platform}") |> Option.defaultValue ""

let private formatCpus (operation: GraphDef.ContaineredShellOperation) =
    match operation.Cpus with
    | Some cpus -> $"--cpus={cpus}"
    | _ -> ""

let private formatContainerEnvs (operation: GraphDef.ContaineredShellOperation) containerHome =
    let matcher = Matcher()
    matcher.AddIncludePatterns(operation.Variables)
    let fixedEnvs =
        [ "HOME", containerHome
          "TERRABUILD_HOME", containerHome
          "TMPDIR", containerTmp ]
        |> List.map (fun (key, value) -> $"-e {key}={value}")

    let passthroughEnvs =
        envVars()
        |> Seq.choose (fun entry ->
            let key = entry.Key
            let value = entry.Value
            if matcher.Match([ key ]).HasMatches then
                let expandedValue = value |> expandTerrabuildHome containerHome
                if value = expandedValue then Some $"-e {key}"
                else Some $"-e {key}={expandedValue}"
            else None)
        |> List.ofSeq

    [ yield! fixedEnvs
      yield! passthroughEnvs
      yield! (operation.Envs.Keys |> Seq.map (fun key -> $"-e {key}")) ]
    |> String.join " "

let private buildHostCommand (operation: GraphDef.ContaineredShellOperation) projectDirectory : BuiltCommand =
    operation.MetaCommand, projectDirectory, operation.Command, operation.Arguments, operation.Image, operation.ErrorLevel, operation.Envs

let private requiresContainerSocket (command: string) =
    let fileName = command |> Path.GetFileName
    fileName = "docker"

let private formatDockerMount source target =
    $"-v {source}:{target}"

let private formatPodmanMount source target =
    $"--mount type=bind,src={source},target={target}"

let private buildDockerPolicy (runtime: HostRuntime) (operation: GraphDef.ContaineredShellOperation) homeDir tmpDir wsDir =
    let extraArgs =
        [ "--net=host"
          "--pid=host"
          "--ipc=host"
          match runtime.Platform, runtime.UserId, runtime.GroupId with
          | Environment.HostPlatform.Linux, Some userId, Some groupId -> $"--user {userId}:{groupId}"
          | _ -> ()
          if requiresContainerSocket operation.Command then
              "-v /var/run/docker.sock:/var/run/docker.sock" ]

    let mountArgs =
        [ formatDockerMount homeDir containerHome
          formatDockerMount tmpDir containerTmp
          formatDockerMount wsDir "/terrabuild" ]

    { EngineCommand = "docker"
      ExtraArgs = extraArgs
      MountArgs = mountArgs }

let private buildPodmanPolicy (runtime: HostRuntime) (operation: GraphDef.ContaineredShellOperation) homeDir tmpDir wsDir =
    let extraArgs =
        [ "--net=host"
          "--pid=host"
          "--ipc=host"
          match runtime.Platform with
          | Environment.HostPlatform.Linux ->
              "--userns=keep-id"
              "--security-opt"
              "label=disable"
          | _ -> () ]

    let mountArgs =
        [ formatPodmanMount homeDir containerHome
          formatPodmanMount tmpDir containerTmp
          formatPodmanMount wsDir "/terrabuild" ]

    { EngineCommand = "podman"
      ExtraArgs = extraArgs
      MountArgs = mountArgs }

let private buildContainerPolicy runtime engineRequestPath operation homeDir tmpDir wsDir =
    match engineRequestPath with
    | EngineRequestPath.Docker -> buildDockerPolicy runtime operation homeDir tmpDir wsDir
    | EngineRequestPath.Podman -> buildPodmanPolicy runtime operation homeDir tmpDir wsDir
    | EngineRequestPath.Host -> invalidArg "engineRequestPath" "Host engine does not support container policy"

let private buildContainerCommand runtime engineRequestPath (node: GraphDef.Node) (operation: GraphDef.ContaineredShellOperation) (options: ConfigOptions.Options) projectDirectory homeDir tmpDir : BuiltCommand =
    let wsDir = currentDir()
    let platform = formatPlatform operation
    let cpus = formatCpus operation
    let image = operation.Image.Value
    let envs = formatContainerEnvs operation containerHome
    let policy = buildContainerPolicy runtime engineRequestPath operation homeDir tmpDir wsDir
    let runArgs =
        [ "run"
          "--rm"
          $"--name {node.TargetHash}"
          cpus ]
        @ policy.ExtraArgs
        @ policy.MountArgs
        @ [ $"-w /terrabuild/{projectDirectory}"
            platform
            $"--entrypoint {operation.Command}"
            envs
            image
            operation.Arguments ]
    let args =
        runArgs
        |> List.filter (String.IsNullOrWhiteSpace >> not)
        |> String.join " "

    operation.MetaCommand, options.Workspace, policy.EngineCommand, args, operation.Image, operation.ErrorLevel, operation.Envs

let rec buildCommands (node: GraphDef.Node) (options: ConfigOptions.Options) projectDirectory homeDir tmpDir =
    buildCommandsForRuntime (detectHostRuntime ()) node options projectDirectory homeDir tmpDir

and internal buildCommandsForRuntime (runtime: HostRuntime) (node: GraphDef.Node) (options: ConfigOptions.Options) projectDirectory homeDir tmpDir =
    let enginePath = resolveEngineRequestPath options.Engine

    node.Operations
    |> List.map (fun operation ->
        match enginePath, operation.Image with
        | EngineRequestPath.Docker, Some _ ->
            buildContainerCommand runtime EngineRequestPath.Docker node operation options projectDirectory homeDir tmpDir
        | EngineRequestPath.Podman, Some _ ->
            buildContainerCommand runtime EngineRequestPath.Podman node operation options projectDirectory homeDir tmpDir
        | EngineRequestPath.Host, _
        | _, None ->
            buildHostCommand operation projectDirectory)

let execCommands (node: GraphDef.Node) (cacheEntry: Cache.IEntry) (options: ConfigOptions.Options) projectDirectory homeDir tmpDir =
    let stepLogs = List<Cache.OperationSummary>()
    let mutable lastStatusCode = 0
    let mutable cmdLineIndex = 0
    let cmdFirstStartedAt = DateTime.UtcNow
    let mutable cmdLastEndedAt = cmdFirstStartedAt
    let mutable startedAt = DateTime.UtcNow
    let mutable cmdLastSuccess = true
    let allCommands = buildCommands node options projectDirectory homeDir tmpDir

    while cmdLineIndex < allCommands.Length && cmdLastSuccess do
        startedAt <- if cmdLineIndex > 0 then DateTime.UtcNow else cmdFirstStartedAt
        let metaCommand, workDir, cmd, args, container, errorLevel, envs = allCommands[cmdLineIndex]
        cmdLineIndex <- cmdLineIndex + 1

        Log.Debug("{NodeId}: running '{Command}' with '{Arguments}'", node.Id, cmd, args)
        let logFile = cacheEntry.NextLogFile()

        try
            let exitCode =
                if options.Targets |> Set.contains "serve" then Exec.execConsole workDir cmd args envs
                else Exec.execCaptureTimestampedOutput workDir cmd args envs logFile

            cmdLastEndedAt <- DateTime.UtcNow
            let endedAt = cmdLastEndedAt
            let duration = endedAt - startedAt

            let stepLog =
                { Cache.OperationSummary.MetaCommand = metaCommand
                  Cache.OperationSummary.Command = cmd
                  Cache.OperationSummary.Arguments = args
                  Cache.OperationSummary.Container = container
                  Cache.OperationSummary.StartedAt = startedAt
                  Cache.OperationSummary.EndedAt = endedAt
                  Cache.OperationSummary.Duration = duration
                  Cache.OperationSummary.Log = logFile
                  Cache.OperationSummary.ExitCode = exitCode }
            stepLogs.Add stepLog

            lastStatusCode <- exitCode
            cmdLastSuccess <- exitCode <= errorLevel
            Log.Debug("{NodeId}: execution completed with exit code '{Code}' ({Status})", node.Id, exitCode, lastStatusCode)
        with exn ->
            let exitCode = 5
            cmdLastEndedAt <- DateTime.UtcNow
            cmdLastSuccess <- false

            let endedAt = cmdLastEndedAt
            let duration = endedAt - startedAt
            $"{exn}" |> IO.appendTextFile logFile

            let stepLog =
                { Cache.OperationSummary.MetaCommand = metaCommand
                  Cache.OperationSummary.Command = cmd
                  Cache.OperationSummary.Arguments = args
                  Cache.OperationSummary.Container = container
                  Cache.OperationSummary.StartedAt = startedAt
                  Cache.OperationSummary.EndedAt = endedAt
                  Cache.OperationSummary.Duration = duration
                  Cache.OperationSummary.Log = logFile
                  Cache.OperationSummary.ExitCode = exitCode }
            stepLogs.Add stepLog

            lastStatusCode <- exitCode
            Log.Error(exn, "{NodeId}: Execution failed with exit code '{Code}' ({Status})", node.Id, exitCode, lastStatusCode)

    cmdLastSuccess, lastStatusCode, (stepLogs |> List.ofSeq)

let buildBatchSchedule flattenBatchProgress (graph: GraphDef.Graph) (targetNode: GraphDef.Node) (membersOpt: Set<string> option) =
    [ match membersOpt with
      | Some members ->
          if flattenBatchProgress then
              yield! members |> Seq.map (fun nodeId ->
                  let n = graph.Nodes[nodeId]
                  (n.Id, $"{targetNode.Target} {n.ProjectDir}"))
          else
              (targetNode.Id, $"{targetNode.Target}")
              yield! members |> Seq.map (fun nodeId ->
                  let n = graph.Nodes[nodeId]
                  (n.Id, $" {Ansi.Styles.dimwhite}⦙{Ansi.Styles.reset} {n.ProjectDir}"))
      | None ->
          (targetNode.Id, $"{targetNode.Target} {targetNode.ProjectDir}") ]

let run (options: ConfigOptions.Options) (cache: Cache.ICache) (api: Contracts.IApiClient option) (graph: GraphDef.Graph) =
    let startedAt = DateTime.UtcNow
    let repository =
        options.Repository
        |> Git.tryNormalizeRepositoryIdentity
        |> Option.defaultValue options.Repository
    $"{Ansi.Emojis.rocket} Processing tasks" |> Terminal.writeLine
    let buildProgress = Notification.BuildNotification() :> BuildProgress.IBuildProgress
    let flattenBatchProgress =
        options.LogTypes
        |> List.exists (function
            | Contracts.GitHubActions -> true
            | _ -> false)
    buildProgress.BuildStarted()
    api |> Option.iter (fun api ->
        api.StartBuild()

        let graphNodes =
            graph.Nodes.Values
            |> Seq.sortBy (fun node -> node.Id)
            |> Seq.map (fun node ->
                { Contracts.BuildGraphNode.Id = node.Id
                  Contracts.BuildGraphNode.ProjectId = node.ProjectId
                  Contracts.BuildGraphNode.ProjectName = node.ProjectName
                  Contracts.BuildGraphNode.ProjectDir = node.ProjectDir
                  Contracts.BuildGraphNode.Target = node.Target
                  Contracts.BuildGraphNode.Dependencies = node.Dependencies |> Seq.sort |> List.ofSeq
                  Contracts.BuildGraphNode.Artifacts = string node.Artifacts
                  Contracts.BuildGraphNode.Build = string node.Build
                  Contracts.BuildGraphNode.Batch = string node.Batch
                  Contracts.BuildGraphNode.Action = string node.Action
                  Contracts.BuildGraphNode.Required = node.Required
                  Contracts.BuildGraphNode.IsBatchNode = graph.Batches.ContainsKey(node.Id) })
            |> List.ofSeq
        let graphHash =
            graphNodes
            |> Seq.collect (fun node ->
                seq {
                    yield repository
                    yield node.Id
                    yield node.ProjectId
                    yield node.ProjectName |> Option.defaultValue ""
                    yield node.ProjectDir
                    yield node.Target
                    yield! node.Dependencies
                    yield node.Artifacts
                    yield node.Build
                    yield node.Batch
                    yield node.Action
                    yield string node.Required
                    yield string node.IsBatchNode
                })
            |> Hash.sha256strings

        api.UploadBuildGraph graphHash graphNodes)

    let nodeResults = Concurrent.ConcurrentDictionary<string, TaskRequest * TaskStatus>()
    let scheduledExec = Concurrent.ConcurrentDictionary<string, bool>()
    use hub = Hub.Create(options.MaxConcurrency)

    // member node id -> batch id
    let memberToBatch =
        graph.Batches
        |> Seq.collect (fun (KeyValue(batchId, members)) ->
            members |> Seq.map (fun nodeId -> nodeId, batchId))
        |> Map.ofSeq

    let execId (nodeId: string) =
        memberToBatch |> Map.tryFind nodeId |> Option.defaultValue nodeId

    // ----------------------------
    // actions
    // ----------------------------

    let summaryNode (node: GraphDef.Node) =
        Log.Debug("{NodeId}: downloading Node Summary", node.Id)
        buildProgress.TaskDownloading node.Id

        let useRemote = GraphDef.isRemoteCacheable options node
        let cacheEntryId = GraphDef.buildCacheKey node

        let status =
            match cache.TryGetSummaryOnly useRemote cacheEntryId with
            | Some (_, summary) ->
                api |> Option.iter (fun api -> api.UseArtifact node.ProjectHash node.TargetHash)
                if summary.IsSuccessful then TaskStatus.Success summary.EndedAt
                else TaskStatus.Failure (summary.EndedAt, $"Restored node {node.Id} with a build in failure state")
            | _ ->
                raiseBugError $"Unable to download build output for {cacheEntryId} for node {node.Id}"

        nodeResults[node.Id] <- (TaskRequest.Restore, status)

        match status with
        | TaskStatus.Success completionDate ->
            hub.GetSignal<DateTime>(node.Id).Set completionDate
            buildProgress.TaskCompleted node.Id true true
        | _ ->
            buildProgress.TaskCompleted node.Id true false

    let restoreNode (node: GraphDef.Node) =
        Log.Debug("{NodeId}: restoring Node", node.Id)
        buildProgress.TaskDownloading node.Id

        let projectDirectory =
            match node.ProjectDir with
            | FS.Directory d -> d
            | FS.File f -> f |> FS.parentDirectory |> Option.get
            | _ -> "."

        let useRemote = GraphDef.isRemoteCacheable options node
        let cacheEntryId = GraphDef.buildCacheKey node

        let status =
            match cache.TryGetSummaryOnly useRemote cacheEntryId with
            | Some (_, summary) ->
                match cache.TryGetSummary useRemote cacheEntryId with
                | Some summary ->
                    Log.Debug("{NodeId}: restoring from key '{Key}'", node.Id, GraphDef.buildCacheKey node)
                    match summary.Outputs with
                    | Some outputs ->
                        let files = IO.enumerateFiles outputs
                        IO.copyFiles projectDirectory outputs files |> ignore
                        api |> Option.iter (fun api -> api.UseArtifact node.ProjectHash node.TargetHash)
                    | _ -> ()
                | _ ->
                    raiseBugError $"Unable to download build output for {cacheEntryId} for node {node.Id}"

                if summary.IsSuccessful then TaskStatus.Success summary.EndedAt
                else TaskStatus.Failure (summary.EndedAt, $"Restored node {node.Id} with a build in failure state")
            | _ ->
                TaskStatus.Failure (DateTime.UtcNow, $"Unable to download build output for {cacheEntryId} for node {node.Id}")

        nodeResults[node.Id] <- (TaskRequest.Restore, status)

        match status with
        | TaskStatus.Success completionDate ->
            hub.GetSignal<DateTime>(node.Id).Set completionDate
            buildProgress.TaskCompleted node.Id true true
        | _ ->
            buildProgress.TaskCompleted node.Id true false

    let execNode (node: GraphDef.Node) =
        let startedAt = DateTime.UtcNow
        Log.Debug("{NodeId}: executing Node", node.Id)
        buildProgress.TaskBuilding node.Id

        let projectDirectory = node.ProjectDir
        let useRemote = GraphDef.isRemoteCacheable options node
        let cacheEntryId = GraphDef.buildCacheKey node
        let cacheEntry = cache.GetEntry useRemote cacheEntryId

        let successful, lastStatusCode, stepLogs =
            try execCommands node cacheEntry options projectDirectory options.HomeDir options.TmpDir
            with exn ->
                nodeResults[node.Id] <- (TaskRequest.Exec, TaskStatus.Failure (DateTime.UtcNow, $"{exn}"))
                Log.Error(exn, "{NodeId}: Execution failed with exception", node.Id)
                reraise()

        let outputs =
            match node.Artifacts with
            | GraphDef.ArtifactMode.Workspace
            | GraphDef.ArtifactMode.Managed when node.Outputs <> Set.empty ->
                let afterFiles = IO.createSnapshot node.Outputs projectDirectory
                let newFiles = afterFiles - IO.Snapshot.Empty
                IO.copyFiles cacheEntry.Outputs projectDirectory newFiles
            | _ -> None

        let endedAt = DateTime.UtcNow

        hub.SubscribeBackground $"Upload {node.Id}" [] (fun () ->
            buildProgress.TaskUploading node.Id

            let summary =
                { Cache.TargetSummary.Project = node.ProjectDir
                  Cache.TargetSummary.Target = node.Target
                  Cache.TargetSummary.Operations = [ stepLogs ]
                  Cache.TargetSummary.Outputs = outputs
                  Cache.TargetSummary.IsSuccessful = successful
                  Cache.TargetSummary.StartedAt = startedAt
                  Cache.TargetSummary.EndedAt = endedAt
                  Cache.TargetSummary.Duration = endedAt - startedAt
                  Cache.TargetSummary.Cache = node.Artifacts }

            Log.Debug("{NodeId}: building '{Key}'", node.Id, GraphDef.buildCacheKey node)
            let files = cacheEntry.Complete summary
            api |> Option.iter (fun api -> api.AddArtifact node.ProjectDir node.Target node.ProjectHash node.TargetHash files successful startedAt endedAt)

            let status =
                if successful then TaskStatus.Success endedAt
                else TaskStatus.Failure (endedAt, $"{node.Id} failed with exit code {lastStatusCode}")

            nodeResults[node.Id] <- (TaskRequest.Exec, status)

            match status with
            | TaskStatus.Success completionDate ->
                buildProgress.TaskCompleted node.Id false true
                hub.GetSignal<DateTime>(node.Id).Set completionDate
            | _ ->
                buildProgress.TaskCompleted node.Id false false
        )

    let batchExecNode (batchNode: GraphDef.Node) =
        let startedAt = DateTime.UtcNow
        Log.Debug("{NodeId}: executing batch", batchNode.Id)
        if not flattenBatchProgress then
            buildProgress.TaskBuilding batchNode.Id

        let batchId = batchNode.Id
        let members = graph.Batches[batchId]

        let beforeFiles =
            members
            |> Seq.map (fun nodeId ->
                let node = graph.Nodes[nodeId]
                buildProgress.TaskBuilding node.Id

                let useRemote = GraphDef.isRemoteCacheable options node
                let cacheEntryId = GraphDef.buildCacheKey node
                let cacheEntry = cache.GetEntry useRemote cacheEntryId
                node.Id, cacheEntry)
            |> Map.ofSeq

        let batchCacheEntryId = GraphDef.buildCacheKey batchNode
        let batchCacheEntry = cache.GetEntry false batchCacheEntryId

        let successful, lastStatusCode, stepLogs =
            try execCommands batchNode batchCacheEntry options batchNode.ProjectDir options.HomeDir options.TmpDir
            with exn ->
                beforeFiles
                |> Map.iter (fun nodeId _ -> nodeResults[nodeId] <- (TaskRequest.Exec, TaskStatus.Failure (DateTime.UtcNow, $"{exn}")))
                Log.Error(exn, "{NodeId}: Execution failed with exception", batchNode.Id)
                reraise()

        let endedAt = DateTime.UtcNow
        let duration = (endedAt - startedAt).Ticks / (members.Count |> int64) |> TimeSpan

        let status =
            if successful then TaskStatus.Success endedAt
            else TaskStatus.Failure (endedAt, $"{batchNode.Id} failed with exit code {lastStatusCode}")

        // async upload summaries for each member node
        beforeFiles
        |> Map.iter (fun nodeId cacheEntry ->
            hub.SubscribeBackground $"upload {nodeId}" [] (fun () ->
                let node = graph.Nodes[nodeId]
                buildProgress.TaskUploading node.Id

                match node.Action with
                | GraphDef.RunAction.Restore ->
                    nodeResults[nodeId] <- (TaskRequest.Restore, status)
                    api |> Option.iter (fun api -> api.UseArtifact node.ProjectHash node.TargetHash)
                | _ ->
                    // copy logs only for members that publish a new cache entry
                    let logs = stepLogs |> List.map (fun stepLog -> stepLog.Log)
                    IO.copyFiles cacheEntry.Logs batchCacheEntry.Logs logs |> ignore

                    let outputs =
                        match node.Artifacts with
                        | GraphDef.ArtifactMode.Workspace
                        | GraphDef.ArtifactMode.Managed when node.Outputs <> Set.empty ->
                            let newFiles = IO.createSnapshot node.Outputs node.ProjectDir - IO.Snapshot.Empty
                            IO.copyFiles cacheEntry.Outputs node.ProjectDir newFiles
                        | _ -> None

                    let summary =
                        { Cache.TargetSummary.Project = node.ProjectDir
                          Cache.TargetSummary.Target = node.Target
                          Cache.TargetSummary.Operations = [ stepLogs ]
                          Cache.TargetSummary.Outputs = outputs
                          Cache.TargetSummary.IsSuccessful = successful
                          Cache.TargetSummary.StartedAt = startedAt
                          Cache.TargetSummary.EndedAt = endedAt
                          Cache.TargetSummary.Duration = duration
                          Cache.TargetSummary.Cache = node.Artifacts }

                    nodeResults[nodeId] <- (TaskRequest.Exec, status)

                    let files = cacheEntry.Complete summary
                    api |> Option.iter (fun api -> api.AddArtifact node.ProjectDir node.Target node.ProjectHash node.TargetHash files successful startedAt endedAt)

                match status with
                | TaskStatus.Success completionDate ->
                    Log.Debug("{NodeId} is successful", nodeId)
                    buildProgress.TaskCompleted nodeId false true
                    hub.GetSignal<DateTime>(nodeId).Set completionDate
                | _ ->
                    Log.Debug("{NodeId} has failed", nodeId)
                    buildProgress.TaskCompleted nodeId false false
            )
        )

        match status with
        | TaskStatus.Success _ ->
            Log.Debug("{NodeId} is successful", batchNode.Id)
            if not flattenBatchProgress then
                buildProgress.TaskCompleted batchNode.Id false true
        | _ ->
            Log.Debug("{NodeId} has failed", batchNode.Id)
            if not flattenBatchProgress then
                buildProgress.TaskCompleted batchNode.Id false false

    // ----------------------------
    // scheduling
    // ----------------------------

    let rec scheduleNode (node: GraphDef.Node) =
        let id = execId node.Id

        if scheduledExec.TryAdd(id, true) then
            let targetNode = graph.Nodes[id]

            // placeholder MUST be keyed by exec id
            nodeResults[id] <- (TaskRequest.Exec, TaskStatus.Failure (DateTime.UtcNow, "Task execution not yet completed"))

            let membersOpt = graph.Batches |> Map.tryFind id

            let schedDependencies =
                targetNode.Dependencies
                |> Seq.choose (fun depId ->
                    // happily non-required nodes
                    let node = graph.Nodes[depId]
                    if node.Required then
                        scheduleNode graph.Nodes[depId]
                        hub.GetSignal<DateTime>(depId) |> Some
                    else
                        None)
                |> List.ofSeq

            let subscribe =
                match targetNode.Action with
                | GraphDef.RunAction.Exec -> hub.Subscribe
                | GraphDef.RunAction.Restore -> hub.SubscribeBackground
                | GraphDef.RunAction.Summary -> hub.SubscribeBackground
                | GraphDef.RunAction.Ignore -> hub.SubscribeBackground

            subscribe targetNode.Id schedDependencies (fun () ->
                let batchSchedule = buildBatchSchedule flattenBatchProgress graph targetNode membersOpt
                buildProgress.BatchScheduled batchSchedule

                match targetNode.Action with
                | GraphDef.RunAction.Exec ->
                    let action = if membersOpt.IsSome then batchExecNode else execNode
                    action targetNode
                | GraphDef.RunAction.Restore -> restoreNode targetNode
                | GraphDef.RunAction.Summary -> summaryNode targetNode
                | GraphDef.RunAction.Ignore -> ()
            )

    // schedule root nodes (exec id indirection handled in scheduleNode)
    graph.RootNodes
    |> Seq.iter (fun nodeId -> scheduleNode graph.Nodes[nodeId])

    let status = hub.WaitCompletion()
    buildProgress.BuildCompleted()

    match status with
    | Status.Ok -> Log.Debug("Build successful")
    | Status.UnfulfilledSubscription (subscription, signals) ->
        let unraisedSignals = signals |> String.join ","
        Log.Fatal("Task '{Subscription}' has pending operations on '{UnraisedSignals}'", subscription, unraisedSignals)
    | Status.SubscriptionError edi ->
        Log.Fatal(edi.SourceException, "Build failed")
        forwardInvalidArg("Failed to build", edi.SourceException)

    let headCommit = options.HeadCommit
    let branchOrTag = options.BranchOrTag

    let nodeStatus =
        let getDependencyStatus _ (node: GraphDef.Node) =
            match nodeResults.TryGetValue node.Id with
            | true, (request, st) ->
                { NodeInfo.Request = request
                  NodeInfo.Status = st
                  NodeInfo.Project = node.ProjectDir
                  NodeInfo.Target = node.Target
                  NodeInfo.ProjectHash = node.ProjectHash
                  NodeInfo.TargetHash = node.TargetHash }
                |> Some
            | _ -> None

        graph.Nodes |> Map.choose getDependencyStatus

    if nodeResults.Count = 0 then
        $" {Ansi.Styles.green}{Ansi.Emojis.arrow}{Ansi.Styles.reset} Everything's up to date"
        |> Terminal.writeLine

    let isSuccess =
        graph.RootNodes
        |> Set.forall (fun nodeId ->
            match nodeStatus |> Map.tryFind nodeId with
            | Some info -> info.Status.IsSuccess
            | _ -> false)

    let buildInfo =
        { Summary.Commit = headCommit.Sha
          Summary.BranchOrTag = branchOrTag
          Summary.StartedAt = startedAt
          Summary.EndedAt = DateTime.UtcNow
          Summary.IsSuccess = isSuccess
          Summary.Targets = options.Targets
          Summary.Nodes = nodeStatus }

    api |> Option.iter (fun api -> api.CompleteBuild buildInfo.IsSuccess)
    buildInfo

let loadSummary (options: ConfigOptions.Options) (cache: Cache.ICache) (graph: GraphDef.Graph) =
    let startedAt = DateTime.UtcNow
    let allowRemoteCache = options.LocalOnly |> not

    let nodeStatus =
        let getDependencyStatus _ (node: GraphDef.Node) =
            let cacheEntryId = GraphDef.buildCacheKey node
            match cache.TryGetSummary allowRemoteCache cacheEntryId with
            | Some summary ->
                let status =
                    if summary.IsSuccessful then TaskStatus.Success summary.EndedAt
                    else TaskStatus.Failure (summary.EndedAt, "logs")

                { NodeInfo.Request = TaskRequest.Restore
                  NodeInfo.Status = status
                  NodeInfo.Project = node.ProjectDir
                  NodeInfo.Target = node.Target
                  NodeInfo.ProjectHash = node.ProjectHash
                  NodeInfo.TargetHash = node.TargetHash }
                |> Some
            | _ -> None

        graph.Nodes |> Map.choose getDependencyStatus

    let isSuccess =
        graph.RootNodes
        |> Set.forall (fun nodeId ->
            match nodeStatus |> Map.tryFind nodeId with
            | Some info -> info.Status.IsSuccess
            | _ -> false)

    let headCommit = options.HeadCommit
    let branchOrTag = options.BranchOrTag
    let endedAt = DateTime.UtcNow

    { Summary.Commit = headCommit.Sha
      Summary.BranchOrTag = branchOrTag
      Summary.StartedAt = startedAt
      Summary.EndedAt = endedAt
      Summary.IsSuccess = isSuccess
      Summary.Targets = options.Targets
      Summary.Nodes = nodeStatus }
