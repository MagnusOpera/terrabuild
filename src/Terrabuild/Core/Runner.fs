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

type private BuiltCommand = string * string * string * Exec.Arguments * string option * int * Map<string, string> * string option

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
    operation.Platform |> Option.map (fun platform -> $"--platform={platform}")

let private formatCpus (operation: GraphDef.ContaineredShellOperation) =
    operation.Cpus |> Option.map (fun cpus -> $"--cpus={cpus}")

let private formatContainerEnvs (operation: GraphDef.ContaineredShellOperation) containerHome =
    let matcher = Matcher()
    matcher.AddIncludePatterns(operation.Variables)
    let fixedEnvs =
        [ "HOME", containerHome
          "TERRABUILD_HOME", containerHome
          "TMPDIR", containerTmp ]
        |> List.collect (fun (key, value) -> [ "-e"; $"{key}={value}" ])

    let passthroughEnvs =
        envVars()
        |> Seq.choose (fun entry ->
            let key = entry.Key
            let value = entry.Value
            if matcher.Match([ key ]).HasMatches then
                let expandedValue = value |> expandTerrabuildHome containerHome
                if value = expandedValue then Some [ "-e"; key ]
                else Some [ "-e"; $"{key}={expandedValue}" ]
            else None)
        |> Seq.collect id
        |> List.ofSeq

    [ yield! fixedEnvs
      yield! passthroughEnvs
      yield! (operation.Envs.Keys |> Seq.collect (fun key -> [ "-e"; key ])) ]

let private buildHostCommand (operation: GraphDef.ContaineredShellOperation) projectDirectory : BuiltCommand =
    operation.MetaCommand, projectDirectory, operation.Command, Exec.Arguments.Raw operation.Arguments, operation.Image, operation.ErrorLevel, operation.Envs, operation.Stdout

let private requiresContainerSocket (command: string) =
    let fileName = command |> Path.GetFileName
    fileName = "docker"

let private formatDockerMount source target =
    [ "-v"; $"{source}:{target}" ]

let private formatPodmanMount source target =
    [ "--mount"; $"type=bind,src={source},target={target}" ]

let private buildDockerPolicy (runtime: HostRuntime) (operation: GraphDef.ContaineredShellOperation) homeDir tmpDir wsDir =
    let extraArgs =
        [ "--net=host"
          "--pid=host"
          "--ipc=host"
          match runtime.Platform, runtime.UserId, runtime.GroupId with
          | Environment.HostPlatform.Linux, Some userId, Some groupId ->
              "--user"
              $"{userId}:{groupId}"
          | _ -> ()
          if requiresContainerSocket operation.Command then
              "-v"
              "/var/run/docker.sock:/var/run/docker.sock" ]

    let mountArgs =
        [ yield! formatDockerMount homeDir containerHome
          yield! formatDockerMount tmpDir containerTmp
          yield! formatDockerMount wsDir "/terrabuild" ]

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
        [ yield! formatPodmanMount homeDir containerHome
          yield! formatPodmanMount tmpDir containerTmp
          yield! formatPodmanMount wsDir "/terrabuild" ]

    { EngineCommand = "podman"
      ExtraArgs = extraArgs
      MountArgs = mountArgs }

let private buildContainerPolicy runtime engineRequestPath operation homeDir tmpDir wsDir =
    match engineRequestPath with
    | EngineRequestPath.Docker -> buildDockerPolicy runtime operation homeDir tmpDir wsDir
    | EngineRequestPath.Podman -> buildPodmanPolicy runtime operation homeDir tmpDir wsDir
    | EngineRequestPath.Host -> invalidArg "engineRequestPath" "Host engine does not support container policy"

let private buildContainerCommand runtime engineRequestPath (node: GraphDef.Node) (operation: GraphDef.ContaineredShellOperation) (options: ConfigOptions.Options) projectDirectory homeDir tmpDir : BuiltCommand =
    let wsDir = options.Workspace
    let platform = formatPlatform operation
    let cpus = formatCpus operation
    let image = operation.Image.Value
    let envs = formatContainerEnvs operation containerHome
    let policy = buildContainerPolicy runtime engineRequestPath operation homeDir tmpDir wsDir
    let nodeSlug = node.Id |> String.slugify |> String.cut 35
    let nonce = Guid.NewGuid().ToString("N").Substring(0, 12)
    let containerName = $"terrabuild-{nodeSlug}-{nonce}"
    let runArgs =
        [ "run"
          "--rm"
          "--name"
          containerName
          yield! cpus |> Option.toList ]
        @ policy.ExtraArgs
        @ policy.MountArgs
        @ [ "-w"
            $"/terrabuild/{projectDirectory}"
            yield! platform |> Option.toList
            "--entrypoint"
            operation.Command
            yield! envs
            image
            yield! operation.Arguments |> String.splitShellArgs ]

    operation.MetaCommand, options.Workspace, policy.EngineCommand, Exec.Arguments.List runArgs, operation.Image, operation.ErrorLevel, operation.Envs, operation.Stdout

let rec buildCommands (node: GraphDef.Node) (options: ConfigOptions.Options) projectDirectory homeDir tmpDir =
    buildCommandsForRuntime (detectHostRuntime ()) node options projectDirectory homeDir tmpDir

and internal buildCommandsForRuntime (runtime: HostRuntime) (node: GraphDef.Node) (options: ConfigOptions.Options) projectDirectory homeDir tmpDir =
    let enginePath = options.Engine

    node.Operations
    |> List.map (fun operation ->
        match enginePath, operation.Image with
        | ConfigOptions.Engine.Docker, Some _ ->
            buildContainerCommand runtime EngineRequestPath.Docker node operation options projectDirectory homeDir tmpDir
        | ConfigOptions.Engine.Podman, Some _ ->
            buildContainerCommand runtime EngineRequestPath.Podman node operation options projectDirectory homeDir tmpDir
        | _ ->
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
        let metaCommand, workDir, cmd, arguments, container, errorLevel, envs, stdout = allCommands[cmdLineIndex]
        let args = Exec.renderArguments arguments
        cmdLineIndex <- cmdLineIndex + 1

        Log.Debug("{NodeId}: running '{Command}' with '{Arguments}'", node.Id, cmd, args)
        let logFile = cacheEntry.NextLogFile()

        try
            let exitCode, capturedStdout =
                if options.Targets |> Set.contains "serve" && stdout.IsNone then
                    Exec.execConsoleArguments workDir cmd arguments envs, None
                else
                    Exec.execCaptureTimestampedOutputArguments workDir cmd arguments envs logFile stdout.IsSome

            if exitCode <= errorLevel then
                match stdout, capturedStdout with
                | Some destination, Some output ->
                    let destination = Path.GetFullPath(Path.Combine(projectDirectory, destination))
                    let directory =
                        match Path.GetDirectoryName(destination) with
                        | NonNull value -> value
                        | Null -> raiseBugError $"Unable to resolve stdout destination directory for '{destination}'"
                    let temporary = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp")
                    try
                        File.WriteAllText(temporary, output)
                        File.Move(temporary, destination, true)
                    finally
                        if File.Exists(temporary) then
                            File.Delete(temporary)
                | _ -> ()

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

let run (options: ConfigOptions.Options) (cache: Cache.ICache) (api: Contracts.IApiClient option) (uploadGraph: GraphDef.Graph) (graph: GraphDef.Graph) =
    let startedAt = DateTime.UtcNow
    let graphEnvironment = options.Environment |> Option.defaultValue ""
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
            uploadGraph.Nodes.Values
            |> Seq.sortBy (fun node -> node.Id)
            |> Seq.map (fun node ->
                { Contracts.BuildGraphNode.Id = node.Id
                  Contracts.BuildGraphNode.ProjectId = node.ProjectId
                  Contracts.BuildGraphNode.ProjectName = node.ProjectName
                  Contracts.BuildGraphNode.ProjectDir = node.ProjectDir
                  Contracts.BuildGraphNode.Target = node.Target
                  Contracts.BuildGraphNode.Phase = node.Phase
                  Contracts.BuildGraphNode.ProjectHash = node.ProjectHash
                  Contracts.BuildGraphNode.TargetHash = node.TargetHash
                  Contracts.BuildGraphNode.Dependencies = node.Dependencies |> Seq.sort |> List.ofSeq
                  Contracts.BuildGraphNode.Artifacts = string node.Artifacts
                  Contracts.BuildGraphNode.Build = string node.Build
                  Contracts.BuildGraphNode.Batch = string node.Batch
                  Contracts.BuildGraphNode.Action = string node.Action
                  Contracts.BuildGraphNode.Required = node.Required
                  Contracts.BuildGraphNode.IsBatchNode = uploadGraph.Batches.ContainsKey(node.Id) })
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
                    yield node.Phase |> Option.defaultValue ""
                    yield! node.Dependencies
                    yield node.Artifacts
                    yield node.Build
                    yield node.Batch
                    yield node.Action
                    yield string node.Required
                    yield string node.IsBatchNode
                })
            |> Hash.sha256strings

        api.UploadBuildGraph graphHash graphEnvironment graphNodes)

    let nodeResults = Concurrent.ConcurrentDictionary<string, TaskRequest * TaskStatus>()
    let scheduledExec = Concurrent.ConcurrentDictionary<string, bool>()
    use hub = Hub.Create(options.MaxConcurrency)

    let acquireTargetLocks taskId progressIds locks =
        if locks |> Set.isEmpty then
            TargetLock.acquire locks
        else
            DiagnosticsTelemetry.recordTask taskId "lock-wait-started"
            progressIds |> Seq.iter buildProgress.TaskWaitingForLock
            try
                let lease = TargetLock.acquire locks
                DiagnosticsTelemetry.recordTask taskId "lock-acquired"
                lease
            with _ ->
                DiagnosticsTelemetry.recordTask taskId "lock-failed"
                reraise()

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
        DiagnosticsTelemetry.recordTask node.Id "summary-started"
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
        DiagnosticsTelemetry.recordTask node.Id "summary-ended"

    let restoreNode (node: GraphDef.Node) =
        let restoreLocks =
            match node.Artifacts with
            | GraphDef.ArtifactMode.Workspace
            | GraphDef.ArtifactMode.Managed when node.Outputs <> Set.empty -> node.Locks
            | _ -> Set.empty
        use _targetLocks = acquireTargetLocks node.Id [ node.Id ] restoreLocks
        DiagnosticsTelemetry.recordTask node.Id "restore-started"
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
            match cache.Restore useRemote cacheEntryId node.Outputs projectDirectory with
            | Some summary ->
                api |> Option.iter (fun api -> api.UseArtifact node.ProjectHash node.TargetHash)
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
        DiagnosticsTelemetry.recordTask node.Id "restore-ended"

    let execNode (node: GraphDef.Node) =
        use _targetLocks = acquireTargetLocks node.Id [ node.Id ] node.Locks
        DiagnosticsTelemetry.recordTask node.Id "execution-started"
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
                DiagnosticsTelemetry.recordTask node.Id "execution-failed"
                Log.Error(exn, "{NodeId}: Execution failed with exception", node.Id)
                reraise()

        let hasOutputs =
            match node.Artifacts with
            | GraphDef.ArtifactMode.Workspace
            | GraphDef.ArtifactMode.Managed when node.Outputs <> Set.empty ->
                let afterFiles = IO.createSnapshot node.Outputs projectDirectory
                let newFiles = afterFiles - IO.Snapshot.Empty
                cacheEntry.StoreOutputs projectDirectory newFiles |> Option.isSome
            | _ -> false

        let endedAt = DateTime.UtcNow
        DiagnosticsTelemetry.recordTask node.Id "execution-ended"

        hub.SubscribeBackground $"Upload {node.Id}" [] (fun () ->
            DiagnosticsTelemetry.recordTask node.Id "upload-started"
            buildProgress.TaskUploading node.Id

            let summary =
                { Cache.TargetSummary.Project = node.ProjectDir
                  Cache.TargetSummary.Target = node.Target
                  Cache.TargetSummary.Operations = [ stepLogs ]
                  Cache.TargetSummary.HasOutputs = hasOutputs
                  Cache.TargetSummary.IsSuccessful = successful
                  Cache.TargetSummary.StartedAt = startedAt
                  Cache.TargetSummary.EndedAt = endedAt
                  Cache.TargetSummary.Duration = endedAt - startedAt
                  Cache.TargetSummary.Cache = node.Artifacts }

            let files = cacheEntry.Complete summary
            api |> Option.iter (fun api -> api.AddArtifact node.ProjectDir node.ProjectName node.Target node.ProjectHash node.TargetHash files successful startedAt endedAt)

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
            DiagnosticsTelemetry.recordTask node.Id "upload-ended"
        )

    let batchExecNode (batchNode: GraphDef.Node) =
        let batchId = batchNode.Id
        let members = graph.Batches[batchId]
        let progressIds = if flattenBatchProgress then members else Set [ batchNode.Id ]
        use _targetLocks = acquireTargetLocks batchNode.Id progressIds batchNode.Locks
        DiagnosticsTelemetry.recordTask batchNode.Id "batch-started"
        let startedAt = DateTime.UtcNow
        Log.Debug("{NodeId}: executing batch", batchNode.Id)
        if not flattenBatchProgress then
            buildProgress.TaskBuilding batchNode.Id

        let cacheEntries =
            members
            |> Seq.map (fun nodeId ->
                let node = graph.Nodes[nodeId]
                buildProgress.TaskBuilding node.Id

                let cacheEntry =
                    match node.Action with
                    | GraphDef.RunAction.Restore ->
                        None
                    | _ ->
                        let useRemote = GraphDef.isRemoteCacheable options node
                        let cacheEntryId = GraphDef.buildCacheKey node
                        cache.GetEntry useRemote cacheEntryId |> Some

                node.Id, cacheEntry)
            |> Map.ofSeq

        let batchCacheEntryId = GraphDef.buildCacheKey batchNode
        let batchCacheEntry = cache.GetEntry false batchCacheEntryId

        let successful, lastStatusCode, stepLogs =
            try execCommands batchNode batchCacheEntry options batchNode.ProjectDir options.HomeDir options.TmpDir
            with exn ->
                cacheEntries
                |> Map.iter (fun nodeId _ -> nodeResults[nodeId] <- (TaskRequest.Exec, TaskStatus.Failure (DateTime.UtcNow, $"{exn}")))
                DiagnosticsTelemetry.recordTask batchNode.Id "batch-failed"
                Log.Error(exn, "{NodeId}: Execution failed with exception", batchNode.Id)
                reraise()

        let endedAt = DateTime.UtcNow
        DiagnosticsTelemetry.recordTask batchNode.Id "batch-ended"
        let duration = (endedAt - startedAt).Ticks / (members.Count |> int64) |> TimeSpan

        let status =
            if successful then TaskStatus.Success endedAt
            else TaskStatus.Failure (endedAt, $"{batchNode.Id} failed with exit code {lastStatusCode}")

        // Snapshot outputs and stage logs while the batch still owns its target locks.
        let preparedEntries =
            cacheEntries
            |> Map.map (fun nodeId cacheEntry ->
                let node = graph.Nodes[nodeId]
                match node.Action, cacheEntry with
                | GraphDef.RunAction.Restore, _ -> None
                | _, Some cacheEntry ->
                    let logs = stepLogs |> List.map (fun stepLog -> stepLog.Log)
                    cacheEntry.StoreLogs logs

                    let hasOutputs =
                        match node.Artifacts with
                        | GraphDef.ArtifactMode.Workspace
                        | GraphDef.ArtifactMode.Managed when node.Outputs <> Set.empty ->
                            let newFiles = IO.createSnapshot node.Outputs node.ProjectDir - IO.Snapshot.Empty
                            cacheEntry.StoreOutputs node.ProjectDir newFiles |> Option.isSome
                        | _ -> false

                    let summary =
                        { Cache.TargetSummary.Project = node.ProjectDir
                          Cache.TargetSummary.Target = node.Target
                          Cache.TargetSummary.Operations = [ stepLogs ]
                          Cache.TargetSummary.HasOutputs = hasOutputs
                          Cache.TargetSummary.IsSuccessful = successful
                          Cache.TargetSummary.StartedAt = startedAt
                          Cache.TargetSummary.EndedAt = endedAt
                          Cache.TargetSummary.Duration = duration
                          Cache.TargetSummary.Cache = node.Artifacts }

                    Some (cacheEntry, summary)
                | _, None ->
                    raiseBugError $"No cache entry created for executing batch member {node.Id}")

        // Complete cache publication asynchronously after member data is safely staged.
        preparedEntries
        |> Map.iter (fun nodeId preparedEntry ->
            hub.SubscribeBackground $"upload {nodeId}" [] (fun () ->
                DiagnosticsTelemetry.recordTask nodeId "upload-started"
                let node = graph.Nodes[nodeId]
                buildProgress.TaskUploading node.Id

                match node.Action, preparedEntry with
                | GraphDef.RunAction.Restore, _ ->
                    nodeResults[nodeId] <- (TaskRequest.Restore, status)
                    api |> Option.iter (fun api -> api.UseArtifact node.ProjectHash node.TargetHash)
                | _, Some (cacheEntry, summary) ->
                    nodeResults[nodeId] <- (TaskRequest.Exec, status)

                    let files = cacheEntry.Complete summary
                    api |> Option.iter (fun api -> api.AddArtifact node.ProjectDir node.ProjectName node.Target node.ProjectHash node.TargetHash files successful startedAt endedAt)
                | _, None ->
                    raiseBugError $"No cache entry created for executing batch member {node.Id}"

                match status with
                | TaskStatus.Success completionDate ->
                    Log.Debug("{NodeId} is successful", nodeId)
                    buildProgress.TaskCompleted nodeId false true
                    hub.GetSignal<DateTime>(nodeId).Set completionDate
                | _ ->
                    Log.Debug("{NodeId} has failed", nodeId)
                    buildProgress.TaskCompleted nodeId false false
                DiagnosticsTelemetry.recordTask nodeId "upload-ended"
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
            DiagnosticsTelemetry.recordTask id "scheduled"

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
                DiagnosticsTelemetry.recordTask id "ready"
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

    let allNodeStatus =
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

    let nodeStatus =
        allNodeStatus
        |> Map.filter (fun nodeId _ -> graph.Batches |> Map.containsKey nodeId |> not)

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
