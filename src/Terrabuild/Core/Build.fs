module Build
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
    | Status
    | Build
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
        match options.ContainerTool, operation.Container with
        | Some cmd, Some container ->
            let wsDir = currentDir()

            // add platform
            let container =
                match operation.ContainerPlatform with
                | Some platform -> $"--platform={platform} {container}"
                | _ -> container

            let containerHome =
                match containerInfos.TryGetValue(container) with
                | true, containerHome ->
                    Log.Debug("Reusing USER {containerHome} for {container}", containerHome, container)
                    containerHome
                | _ ->
                    // discover USER
                    let args = $"run --rm --name {node.TargetHash} --entrypoint sh {container} \"echo -n \\$HOME\""
                    let containerHome =
                        Log.Debug("Identifying USER for {container}", container)
                        match Exec.execCaptureOutput options.Workspace cmd args with
                        | Exec.Success (containerHome, 0) -> containerHome.Trim()
                        | _ ->
                            Log.Debug("USER identification failed for {container}: using root", container)
                            "/root"

                    Log.Debug("Using USER {containerHome} for {container}", containerHome, container)
                    containerInfos.TryAdd(container, containerHome) |> ignore
                    containerHome

            let envs =
                let matcher = Matcher()
                matcher.AddIncludePatterns(operation.ContainerVariables)
                envVars()
                |> Seq.choose (fun entry -> 
                    let key = entry.Key
                    let value = entry.Value
                    if matcher.Match([key]).HasMatches then
                        let expandedValue = value |> expandTerrabuildHome containerHome
                        if value = expandedValue then Some $"-e {key}"
                        else Some $"-e {key}={expandedValue}"
                    else None)
                |> String.join " "
            let args = $"run --rm --name {node.TargetHash} --net=host --pid=host --ipc=host -v /var/run/docker.sock:/var/run/docker.sock -v {homeDir}:{containerHome} -v {tmpDir}:/tmp -v {wsDir}:/terrabuild -w /terrabuild/{projectDirectory} --entrypoint {operation.Command} {envs} {container} {operation.Arguments}"
            metaCommand, options.Workspace, cmd, args, operation.Container
        | _ -> metaCommand, projectDirectory, operation.Command, operation.Arguments, operation.Container)


let execCommands (node: GraphDef.Node) (cacheEntry: Cache.IEntry) (options: ConfigOptions.Options) projectDirectory homeDir tmpDir =
    let stepLogs = List<Cache.OperationSummary>()
    let mutable lastStatusCode = 0
    let mutable cmdLineIndex = 0
    let cmdFirstStartedAt = DateTime.UtcNow
    let mutable cmdLastEndedAt = cmdFirstStartedAt
    let allCommands = buildCommands node options projectDirectory homeDir tmpDir
    while cmdLineIndex < allCommands.Length && lastStatusCode = 0 do
        let startedAt =
            if cmdLineIndex > 0 then DateTime.UtcNow
            else cmdFirstStartedAt
        let metaCommand, workDir, cmd, args, container = allCommands[cmdLineIndex]
        cmdLineIndex <- cmdLineIndex + 1

        Log.Debug("{Hash}: Running '{Command}' with '{Arguments}'", node.TargetHash, cmd, args)
        let logFile = cacheEntry.NextLogFile()
        let exitCode =
            if options.Targets |> Set.contains "serve" then
                Exec.execConsole workDir cmd args
            else
                Exec.execCaptureTimestampedOutput workDir cmd args logFile
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
        stepLog |> stepLogs.Add

        lastStatusCode <- exitCode
        Log.Debug("{Hash}: Execution completed with exit code '{Code}' ({Status})", node.TargetHash, exitCode, lastStatusCode)

    lastStatusCode, (stepLogs |> List.ofSeq)



let run (options: ConfigOptions.Options) (cache: Cache.ICache) (api: Contracts.IApiClient option) (graph: GraphDef.Graph) =
    let startedAt = DateTime.UtcNow
    $"{Ansi.Emojis.rocket} Processing tasks" |> Terminal.writeLine

    let buildProgress = Notification.BuildNotification() :> BuildProgress.IBuildProgress
    buildProgress.BuildStarted()
    api |> Option.iter (fun api -> api.StartBuild())

    let allowRemoteCache = options.LocalOnly |> not
    let nodeResults = Concurrent.ConcurrentDictionary<string, TaskRequest * TaskStatus>()
    let scheduledClusters = Concurrent.ConcurrentDictionary<string, bool>()
    let hub = Hub.Create(options.MaxConcurrency)

    let rec restoreNode (node: GraphDef.Node) =
        buildProgress.TaskDownloading node.Id

        let projectDirectory =
            match node.ProjectDir with
            | FS.Directory projectDirectory -> projectDirectory
            | FS.File projectFile -> projectFile |> FS.parentDirectory |> Option.get
            | _ -> "."

        let cacheEntryId = GraphDef.buildCacheKey node
        let status =
            match cache.TryGetSummaryOnly allowRemoteCache cacheEntryId with
            | Some (_, summary) ->
                match cache.TryGetSummary allowRemoteCache cacheEntryId with
                | Some summary ->
                    Log.Debug("{NodeId} restoring '{Project}/{Target}' from {Hash}", node.Id, node.ProjectDir, node.Target, node.TargetHash)
                    match summary.Outputs with
                    | Some outputs ->
                        let files = IO.enumerateFiles outputs
                        IO.copyFiles projectDirectory outputs files |> ignore
                        api |> Option.iter (fun api -> api.UseArtifact node.ProjectHash node.TargetHash)
                    | _ -> ()
                | _ ->
                    raiseBugError $"Unable to download build output for {cacheEntryId} for node {node.Id}"

                match summary.IsSuccessful with
                | true -> TaskStatus.Success summary.EndedAt
                | _ -> TaskStatus.Failure (summary.EndedAt, $"Restored node {node.Id} with a build in failure state")
            | _ ->
                TaskStatus.Failure (DateTime.UtcNow, $"Unable to download build output for {cacheEntryId} for node {node.Id}")
        nodeResults[node.Id] <- (TaskRequest.Restore, status)
        match status with
        | TaskStatus.Success completionDate ->
            let nodeSignal = hub.GetSignal<DateTime> node.Id
            nodeSignal.Set completionDate
            buildProgress.TaskCompleted node.Id true true
        | _ ->
            buildProgress.TaskCompleted node.Id true false

    and batchBuildNode (batchNode: GraphDef.Node) =
        let startedAt = DateTime.UtcNow

        let cluster = graph.Clusters[batchNode.ClusterHash]
        let beforeFiles =
            cluster.Nodes |> Seq.map (fun nodeId ->
                let node = graph.Nodes[nodeId]
                buildProgress.TaskBuilding node.Id

                let cacheEntryId = GraphDef.buildCacheKey node
                let cacheEntry = cache.GetEntry (node.Cache = Terrabuild.Extensibility.Cacheability.Remote) cacheEntryId
                let snapshot =
                    if node.IsLeaf then IO.Snapshot.Empty
                    else IO.createSnapshot node.Outputs node.ProjectDir
                node.Id, (cacheEntry, snapshot))
            |> Map.ofSeq

        let batchCacheEntryId = GraphDef.buildCacheKey batchNode
        let batchCacheEntry = cache.GetEntry (batchNode.Cache = Terrabuild.Extensibility.Cacheability.Remote) batchCacheEntryId
        let lastStatusCode, stepLogs = execCommands batchNode batchCacheEntry options batchNode.ProjectDir options.HomeDir options.TmpDir

        let successful = lastStatusCode = 0
        let endedAt = DateTime.UtcNow
        let duration = endedAt - startedAt

        let status =
            match lastStatusCode with
            | 0 -> TaskStatus.Success endedAt
            | _ -> TaskStatus.Failure (endedAt, $"{batchNode.Id} failed with exit code {lastStatusCode}")

        // async upload summaries
        beforeFiles
        |> Map.iter (fun nodeId (cacheEntry, beforeFiles) ->
            hub.SubscribeBackground $"upload {nodeId}" [] (fun () ->
                let node = graph.Nodes[nodeId]
                let afterFiles = IO.createSnapshot node.Outputs node.ProjectDir
                let newFiles = afterFiles - beforeFiles
                let outputs = IO.copyFiles cacheEntry.Outputs node.ProjectDir newFiles
                let logs = stepLogs |> List.map (fun stepLog -> stepLog.Log)
                IO.copyFiles cacheEntry.Logs batchCacheEntry.Logs logs |> ignore

                buildProgress.TaskUploading node.Id
                let summary =
                    { Cache.TargetSummary.Project = node.ProjectDir
                      Cache.TargetSummary.Target = node.Target
                      Cache.TargetSummary.Operations = [ stepLogs |> List.ofSeq ]
                      Cache.TargetSummary.Outputs = outputs
                      Cache.TargetSummary.IsSuccessful = successful
                      Cache.TargetSummary.StartedAt = startedAt
                      Cache.TargetSummary.EndedAt = endedAt
                      Cache.TargetSummary.Duration = duration
                      Cache.TargetSummary.Cache = node.Cache }
                nodeResults[nodeId] <- (TaskRequest.Build, status)

                // create an archive with new files
                Log.Debug("{NodeId}: Building '{Project}/{Target}' with {Hash}", node.Id, node.ProjectDir, node.Target, node.TargetHash)
                let files = cacheEntry.Complete summary
                api |> Option.iter (fun api -> api.AddArtifact node.ProjectDir node.Target node.ProjectHash node.TargetHash files successful)
                
                // update status
                match status with
                | TaskStatus.Success completionDate ->
                    buildProgress.TaskCompleted nodeId false true
                    let nodeSignal = hub.GetSignal<DateTime> nodeId
                    nodeSignal.Set completionDate
                | _ ->
                    buildProgress.TaskCompleted nodeId false false))

    and buildNode (node: GraphDef.Node) =
        let startedAt = DateTime.UtcNow
        buildProgress.TaskBuilding node.Id

        let projectDirectory = node.ProjectDir
        let beforeFiles =
            if node.IsLeaf then IO.Snapshot.Empty
            else IO.createSnapshot node.Outputs projectDirectory

        let cacheEntryId = GraphDef.buildCacheKey node
        let cacheEntry = cache.GetEntry (node.Cache = Terrabuild.Extensibility.Cacheability.Remote) cacheEntryId
        let lastStatusCode, stepLogs = execCommands node cacheEntry options projectDirectory options.HomeDir options.TmpDir

        // keep only new or modified files
        let afterFiles = IO.createSnapshot node.Outputs projectDirectory
        let newFiles = afterFiles - beforeFiles
        let outputs = IO.copyFiles cacheEntry.Outputs projectDirectory newFiles

        let successful = lastStatusCode = 0
        let endedAt = DateTime.UtcNow
        // async upload summary
        hub.SubscribeBackground $"Upload {node.Id}" [] (fun () ->
            buildProgress.TaskUploading node.Id

            let summary =
                { Cache.TargetSummary.Project = node.ProjectDir
                  Cache.TargetSummary.Target = node.Target
                  Cache.TargetSummary.Operations = [ stepLogs |> List.ofSeq ]
                  Cache.TargetSummary.Outputs = outputs
                  Cache.TargetSummary.IsSuccessful = successful
                  Cache.TargetSummary.StartedAt = startedAt
                  Cache.TargetSummary.EndedAt = endedAt
                  Cache.TargetSummary.Duration = endedAt - startedAt
                  Cache.TargetSummary.Cache = node.Cache }

            // create an archive with new files
            Log.Debug("{NodeId}: Building '{Project}/{Target}' with {Hash}", node.Id, node.ProjectDir, node.Target, node.TargetHash)
            let files = cacheEntry.Complete summary
            api |> Option.iter (fun api -> api.AddArtifact node.ProjectDir node.Target node.ProjectHash node.TargetHash files successful)

            let status =
                match lastStatusCode with
                | 0 -> TaskStatus.Success endedAt
                | _ -> TaskStatus.Failure (endedAt, $"{node.Id} failed with exit code {lastStatusCode}")
            nodeResults[node.Id] <- (TaskRequest.Build, status)
            match status with
            | TaskStatus.Success completionDate ->
                let nodeSignal = hub.GetSignal<DateTime> node.Id
                nodeSignal.Set completionDate
                buildProgress.TaskCompleted node.Id false true
            | _ ->
                buildProgress.TaskCompleted node.Id false false)

    and scheduleNode (node: GraphDef.Node) =
        if node.Action = GraphDef.NodeAction.BatchBuild then raiseBugError "Unexpected BatchNode in scheduling"

        if scheduledClusters.TryAdd(node.ClusterHash, true) then
            let targetNode =
                match graph.Clusters |> Map.tryFind node.ClusterHash with
                | Some cluster when cluster.Nodes.Count > 1 ->
                    let batchNode = graph.Nodes[node.ClusterHash]
                    cluster.Nodes |> Seq.iter (fun nodeId ->
                        let node = graph.Nodes[nodeId]
                        buildProgress.TaskScheduled node.Id $"[{node.Target}] {node.ProjectDir}")
                    batchNode
                | _ ->
                    buildProgress.TaskScheduled node.Id $"{node.Target} {node.ProjectDir}"
                    node

            let schedDependencies =
                targetNode.Dependencies |> Seq.map (fun depId ->
                    scheduleNode graph.Nodes[depId]
                    hub.GetSignal<DateTime> depId)
                |> List.ofSeq

            hub.Subscribe targetNode.Id schedDependencies (fun () ->
                match targetNode.Action with
                | GraphDef.NodeAction.BatchBuild -> batchBuildNode targetNode
                | GraphDef.NodeAction.Build -> buildNode targetNode
                | GraphDef.NodeAction.Restore -> restoreNode targetNode
                | GraphDef.NodeAction.Ignore -> ())

    // build root nodes (and only those that must be built)
    graph.RootNodes
    |> Seq.iter (fun nodeId ->
        let node = graph.Nodes[nodeId]
        scheduleNode node)


    let status = hub.WaitCompletion()
    buildProgress.BuildCompleted()

    match status with
    | Status.Ok ->
        Log.Debug("Build successful")
    | Status.UnfulfilledSubscription (subscription, signals) ->
        let unraisedSignals = signals |> String.join ","
        Log.Fatal($"Task '{subscription}' has pending operations on '{unraisedSignals}'")
    | Status.SubscriptionError exn ->
        Log.Fatal(exn, "Build failed with exception")

    let headCommit = options.HeadCommit
    let branchOrTag = options.BranchOrTag

    let nodeStatus =
        let getDependencyStatus _ (node: GraphDef.Node) =
            match nodeResults.TryGetValue node.Id with
            | true, (request, status) ->
                { NodeInfo.Request = request
                  NodeInfo.Status = status
                  NodeInfo.Project = node.ProjectDir
                  NodeInfo.Target = node.Target
                  NodeInfo.ProjectHash = node.ProjectHash
                  NodeInfo.TargetHash = node.TargetHash } |> Some
            | _ -> None

        graph.Nodes
        |> Map.choose getDependencyStatus

    let upToDate =
        graph.RootNodes |> Set.forall (fun nodeId ->
            match nodeStatus |> Map.tryFind nodeId with
            | Some info -> info.Request.IsStatus
            | _ -> true)
    if upToDate then
        $" {Ansi.Styles.green}{Ansi.Emojis.arrow}{Ansi.Styles.reset} Everything's up to date" |> Terminal.writeLine

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
                  NodeInfo.TargetHash = node.TargetHash } |> Some
            | _ -> None

        graph.Nodes |> Map.choose getDependencyStatus

    let isSuccess =
        graph.RootNodes |> Set.forall (fun nodeId ->
            match nodeStatus |> Map.tryFind nodeId with
            | Some info -> info.Status.IsSuccess
            | _ -> false)

    let headCommit = options.HeadCommit
    let branchOrTag = options.BranchOrTag

    let endedAt = DateTime.UtcNow
    let buildInfo =
        { Summary.Commit = headCommit.Sha
          Summary.BranchOrTag = branchOrTag
          Summary.StartedAt = startedAt
          Summary.EndedAt = endedAt
          Summary.IsSuccess = isSuccess
          Summary.Targets = options.Targets
          Summary.Nodes = nodeStatus }
    buildInfo

