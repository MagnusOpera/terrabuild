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
    | Build
    | Restore

[<RequireQualifiedAccess>]
type TaskStatus =
    | Success of completionDate:DateTime
    | Failure of completionDate:DateTime * message:string
    | Pending

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



type IBuildNotification =
    abstract WaitCompletion: unit -> unit

    abstract BuildStarted: graph:GraphDef.Graph -> unit
    abstract BuildCompleted: summary:Summary -> unit

    abstract NodeScheduled: node:GraphDef.Node -> unit
    abstract NodeDownloading: node:GraphDef.Node -> unit
    abstract NodeBuilding: node:GraphDef.Node -> unit
    abstract NodeUploading: node:GraphDef.Node -> unit
    abstract NodeCompleted: node:GraphDef.Node -> request:TaskRequest -> success:bool -> unit


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
            let args = $"run --rm --net=host --name {node.TargetHash} --pid=host --ipc=host -v /var/run/docker.sock:/var/run/docker.sock -v {homeDir}:{containerHome} -v {tmpDir}:/tmp -v {wsDir}:/terrabuild -w /terrabuild/{projectDirectory} --entrypoint {operation.Command} {envs} {container} {operation.Arguments}"
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

    lastStatusCode, stepLogs



let run (options: ConfigOptions.Options) (cache: Cache.ICache) (api: Contracts.IApiClient option) (notification: IBuildNotification) (graph: GraphDef.Graph) =
    let startedAt = DateTime.UtcNow
    $"{Ansi.Emojis.rocket} Processing tasks" |> Terminal.writeLine

    notification.BuildStarted graph
    api |> Option.iter (fun api -> api.StartBuild())

    let allowRemoteCache = options.LocalOnly |> not
    let homeDir = Cache.createHome()
    let tmpDir = Cache.createTmp()
    let retry = options.Retry

    let nodeResults = Concurrent.ConcurrentDictionary<string, TaskRequest * TaskStatus>()
    let restorables = Concurrent.ConcurrentDictionary<string, Lazy<string list>>()
    let hub = Hub.Create(options.MaxConcurrency)

    let buildNode (node: GraphDef.Node) =
        let startedAt = DateTime.UtcNow
        notification.NodeBuilding node

        let projectDirectory =
            match node.ProjectDir with
            | FS.Directory projectDirectory -> projectDirectory
            | FS.File projectFile -> projectFile |> FS.parentDirectory |> Option.get
            | _ -> "."

        let beforeFiles =
            if node.IsLeaf then IO.Snapshot.Empty
            else IO.createSnapshot node.Outputs projectDirectory

        let cacheEntryId = GraphDef.buildCacheKey node
        let cacheEntry = cache.GetEntry true cacheEntryId
        let lastStatusCode, stepLogs = execCommands node cacheEntry options projectDirectory homeDir tmpDir

        // keep only new or modified files
        let afterFiles = IO.createSnapshot node.Outputs projectDirectory
        let newFiles = afterFiles - beforeFiles
        let outputs = IO.copyFiles cacheEntry.Outputs projectDirectory newFiles

        let successful = lastStatusCode = 0
        let endedAt = DateTime.UtcNow
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

        notification.NodeUploading node

        // create an archive with new files
        Log.Debug("{NodeId}: Building '{Project}/{Target}' with {Hash}", node.Id, node.ProjectDir, node.Target, node.TargetHash)
        let files = cacheEntry.Complete summary
        api |> Option.iter (fun api -> api.AddArtifact node.ProjectDir node.Target node.ProjectHash node.TargetHash files successful)

        match lastStatusCode with
        | 0 -> TaskStatus.Success endedAt
        | _ -> TaskStatus.Failure (DateTime.UtcNow, $"{node.Id} failed with exit code {lastStatusCode}")

    let restoreNode (node: GraphDef.Node) =
        notification.NodeScheduled node

        let projectDirectory =
            match node.ProjectDir with
            | FS.Directory projectDirectory -> projectDirectory
            | FS.File projectFile -> projectFile |> FS.parentDirectory |> Option.get
            | _ -> "."

        let cacheEntryId = GraphDef.buildCacheKey node
        match cache.TryGetSummaryOnly allowRemoteCache cacheEntryId with
        | Some (_, summary) ->

            let restorableId = $"{node.Id}-download"
            let callback() =
                notification.NodeDownloading node
                let restorableSignal = hub.GetSignal<DateTime> restorableId

                match cache.TryGetSummary allowRemoteCache cacheEntryId with
                | Some summary ->
                    Log.Debug("{NodeId} restoring '{Project}/{Target}' from {Hash}", node.Id, node.ProjectDir, node.Target, node.TargetHash)
                    match summary.Outputs with
                    | Some outputs ->
                        let files = IO.enumerateFiles outputs
                        IO.copyFiles projectDirectory outputs files |> ignore
                        api |> Option.iter (fun api -> api.UseArtifact node.ProjectHash node.TargetHash)
                    | _ -> ()
                    notification.NodeCompleted node TaskRequest.Restore true
                | _ ->
                    notification.NodeCompleted node TaskRequest.Restore false
                    raiseBugError $"Unable to download build output for {cacheEntryId} for node {node.Id}"
                restorableSignal.Value <- DateTime.UtcNow

            let restorable =
                lazy (
                    let dependencies =
                        node.Dependencies |> Seq.choose (fun nodeId -> 
                            match restorables.TryGetValue nodeId with
                            | true, restorable -> Some restorable
                            | _ -> None)
                        |> List.ofSeq

                    let names = dependencies |> List.collect _.Value
                    callback()
                    restorableId :: names
                )

            restorables.TryAdd(node.Id, restorable) |> ignore

            if summary.IsSuccessful then TaskStatus.Success summary.EndedAt
            else TaskStatus.Failure (summary.EndedAt, $"Restored node {node.Id} with a build in failure state")
        | _ ->
            TaskStatus.Failure (DateTime.UtcNow, $"Unable to download build output for {cacheEntryId} for node {node.Id}")


    let computeNodeAction (node: GraphDef.Node) maxCompletionChildren =
        if node.Rebuild then
            Log.Debug("{NodeId} must rebuild because force requested", node.Id)
            TaskRequest.Build

        elif node.Cache <> Terrabuild.Extensibility.Cacheability.Never then
            let cacheEntryId = GraphDef.buildCacheKey node
            match cache.TryGetSummaryOnly allowRemoteCache cacheEntryId with
            | Some (_, summary) ->
                Log.Debug("{NodeId} has existing build summary", node.Id)

                // retry requested and task is failed
                if retry && (not summary.IsSuccessful) then
                    Log.Debug("{NodeId} must rebuild because retry requested and node is failed", node.Id)
                    TaskRequest.Build

                // task is older than children
                elif summary.EndedAt <= maxCompletionChildren then
                    Log.Debug("{NodeId} must rebuild because child is rebuilding", node.Id)
                    TaskRequest.Build

                // task is cached
                else
                    Log.Debug("{NodeId} is marked as used", node.Id)
                    TaskRequest.Restore
            | _ ->
                Log.Debug("{NodeId} must be build since no summary and required", node.Id)
                TaskRequest.Build
        else
            Log.Debug("{NodeId} is not cacheable", node.Id)
            TaskRequest.Build




    let rec schedule nodeId =
        if nodeResults.TryAdd(nodeId, (TaskRequest.Build, TaskStatus.Pending)) then
            let node = graph.Nodes[nodeId]
            let nodeComputed = hub.GetSignal<DateTime> nodeId

            // await dependencies
            let awaitedDependencies =
                node.Dependencies
                |> Seq.map (fun awaitedProjectId ->
                    schedule awaitedProjectId
                    hub.GetSignal<DateTime> awaitedProjectId)
                |> List.ofSeq

            let onDependenciesAvailable () =
                try
                    let maxCompletionChildren =
                        match awaitedDependencies with
                        | [ ] -> DateTime.MinValue
                        | _ -> awaitedDependencies |> Seq.maxBy (fun dep -> dep.Value) |> (fun dep -> dep.Value)

                    let buildRequest = computeNodeAction node maxCompletionChildren

                    let awaitedDownloads =
                        match buildRequest with
                        | TaskRequest.Build
                        | TaskRequest.Restore when node.Restore ->
                            match restorables.TryGetValue nodeId with
                            | true, restorable ->
                                restorable.Value |> List.map (fun entry -> hub.GetSignal<DateTime> entry)
                            | _ -> []
                        | _ -> []

                    let onDownloadsAvailable() =
                        Log.Debug("Downloads for {NodeId} completed", node.Id)
                        let buildAction = 
                            match buildRequest with
                            | TaskRequest.Build -> buildNode
                            | TaskRequest.Restore -> restoreNode
                        let completionStatus = buildAction node 

                        Log.Debug("{NodeId} completed request {Request} with status {Status}", node.Id, buildRequest, completionStatus)
                        let success, completionDate =
                            match completionStatus with
                            | TaskStatus.Success completionDate -> true, completionDate
                            | TaskStatus.Failure (completionDate, _) -> false, completionDate
                            | _ -> raiseBugError "Unexpected pending state"
                        nodeResults[node.Id] <- (buildRequest, completionStatus)
                        notification.NodeCompleted node buildRequest success
                        if success then nodeComputed.Value <- completionDate

                    let awaitedSignals = awaitedDownloads |> List.map (fun entry -> entry :> ISignal)
                    hub.Subscribe nodeId awaitedSignals onDownloadsAvailable

                with
                    exn ->
                        Log.Fatal(exn, "{NodeId} unexpected failure while building", node.Id)
                        nodeResults[node.Id] <- (TaskRequest.Build, TaskStatus.Failure (DateTime.UtcNow, exn.Message))
                        notification.NodeCompleted node TaskRequest.Build false
                        reraise()

            let awaitedSignals = awaitedDependencies |> List.map (fun entry -> entry :> ISignal)
            hub.Subscribe nodeId awaitedSignals onDependenciesAvailable

    graph.RootNodes |> Seq.iter schedule

    let status = hub.WaitCompletion()
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

    let isSuccess =
        graph.RootNodes |> Set.forall (fun nodeId ->
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

    notification.BuildCompleted buildInfo
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

