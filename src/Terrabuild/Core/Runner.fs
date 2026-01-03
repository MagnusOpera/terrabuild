module Runner
open System
open System.Collections.Generic
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

let private containerInfos = Concurrent.ConcurrentDictionary<string, string>()

let buildCommands (node: GraphDef.Node) (options: ConfigOptions.Options) projectDirectory homeDir tmpDir =
    node.Operations |> List.map (fun operation ->
        let metaCommand = operation.MetaCommand
        match options.Engine, operation.Image with
        | Some cmd, Some image ->
            let wsDir = currentDir()

            // add platform
            let platform = operation.Platform |> Option.map (fun platform -> $"--platform={platform}") |> Option.defaultValue ""

            let containerHome =
                match containerInfos.TryGetValue(image) with
                | true, containerHome ->
                    Log.Debug("Reusing USER '{ContainerHome}' for '{Container}'", containerHome, image)
                    containerHome
                | _ ->
                    // discover USER
                    let args = $"run --rm --name {node.TargetHash} {platform} --entrypoint sh {image} -c \"echo -n $HOME\""
                    let containerHome =
                        match Exec.execCaptureOutput options.Workspace cmd args Map.empty with
                        | Exec.Success (containerHome, _) -> containerHome.Trim()
                        | Exec.Error (errMsg, code) ->
                            Log.Debug("USER identification failed for '{Container}' with error '{ErrorMsg}' and code {Code}, using root instead", image, errMsg, code)
                            "/root"

                    Log.Debug("Using USER '{ContainerHome}' for '{Container}'", containerHome, image)
                    containerInfos.TryAdd(image, containerHome) |> ignore
                    containerHome

            let envs =
                let matcher = Matcher()
                matcher.AddIncludePatterns(operation.Variables)
                envVars()
                |> Seq.choose (fun entry ->
                    let key = entry.Key
                    let value = entry.Value
                    if matcher.Match([ key ]).HasMatches then
                        let expandedValue = value |> expandTerrabuildHome containerHome
                        if value = expandedValue then Some $"-e {key}"
                        else Some $"-e {key}={expandedValue}"
                    else None)
                |> Seq.append (operation.Envs.Keys |> Seq.map (fun key -> $"-e {key}"))
                |> String.join " "

            let args =
                $"run --rm --name {node.TargetHash} --net=host --pid=host --ipc=host -v /var/run/docker.sock:/var/run/docker.sock -v {homeDir}:{containerHome} -v {tmpDir}:/tmp -v {wsDir}:/terrabuild -w /terrabuild/{projectDirectory} {platform} --entrypoint {operation.Command} {envs} {image} {operation.Arguments}"
            metaCommand, options.Workspace, cmd, args, operation.Image, operation.ErrorLevel, operation.Envs
        | _ ->
            metaCommand, projectDirectory, operation.Command, operation.Arguments, operation.Image, operation.ErrorLevel, operation.Envs
    )

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

let run (options: ConfigOptions.Options) (cache: Cache.ICache) (api: Contracts.IApiClient option) (graph: GraphDef.Graph) =
    let startedAt = DateTime.UtcNow
    $"{Ansi.Emojis.rocket} Processing tasks" |> Terminal.writeLine

    let buildProgress = Notification.BuildNotification() :> BuildProgress.IBuildProgress
    buildProgress.BuildStarted()
    api |> Option.iter (fun api -> api.StartBuild())

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
            api |> Option.iter (fun api -> api.AddArtifact node.ProjectDir node.Target node.ProjectHash node.TargetHash files successful)

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

                // copy log files
                let logs = stepLogs |> List.map (fun stepLog -> stepLog.Log)
                IO.copyFiles cacheEntry.Logs batchCacheEntry.Logs logs |> ignore

                let outputs =
                    match node.Artifacts with
                    | GraphDef.ArtifactMode.Workspace
                    | GraphDef.ArtifactMode.Managed when node.Outputs <> Set.empty ->
                        let newFiles = IO.createSnapshot node.Outputs node.ProjectDir - IO.Snapshot.Empty
                        IO.copyFiles cacheEntry.Outputs node.ProjectDir newFiles
                    | _ -> None

                buildProgress.TaskUploading node.Id

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
                api |> Option.iter (fun api -> api.AddArtifact node.ProjectDir node.Target node.ProjectHash node.TargetHash files successful)

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
            buildProgress.TaskCompleted batchNode.Id false true
        | _ ->
            Log.Debug("{NodeId} has failed", batchNode.Id)
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
                let batchSchedule =
                    [ match membersOpt with
                      | Some members ->
                          (targetNode.Id, $"{targetNode.Target}")
                          yield! members |> Seq.map (fun nodeId ->
                              let n = graph.Nodes[nodeId]
                              (n.Id, $" {Ansi.Styles.dimwhite}⦙{Ansi.Styles.reset} {n.ProjectDir}"))
                      | None ->
                          (targetNode.Id, $"{targetNode.Target} {targetNode.ProjectDir}") ]

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
            | _ -> true)

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
