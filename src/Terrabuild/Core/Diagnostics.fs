module Diagnostics

open System
open System.IO
open System.Collections.Concurrent
open System.Text
open System.Text.Json.Serialization
open Collections
open Errors

type FileFingerprint = {
    Path: string
    Hash: string
}

type DependencyFingerprint = {
    Id: string
    Hash: string
}

type ProjectFingerprint = {
    Id: string
    Name: string option
    Directory: string
    Files: FileFingerprint list
    FileContentHash: string
    FileNameHash: string
    Dependencies: DependencyFingerprint list
    DependenciesHash: string
    ProjectHash: string
}

type TargetFingerprint = {
    DeclaredTargetHash: string option
    OperationHashes: string list
    DependencyTargetHashes: DependencyFingerprint list
    PhaseDependenciesExcludedFromHash: string list
    TargetHash: string
    CacheKey: string
}

type EnvironmentEntryReport = {
    Name: string
    ValueHash: string
}

type ResolvedOperationReport = {
    MetaCommand: string
    Command: string
    ArgumentsHash: string
    Container: string option
    Platform: string option
    Cpus: int option
    ForwardedVariableNames: string list
    InjectedEnvironment: EnvironmentEntryReport list
}

type NodeReport = {
    Id: string
    ProjectId: string
    ProjectName: string option
    ProjectDirectory: string
    Target: string
    Phase: string option
    Selected: bool
    [<JsonIgnore>]
    SelectionKind: string option
    [<JsonIgnore>]
    Scheduled: bool option
    [<JsonIgnore>]
    Outcome: string option
    [<JsonIgnore>]
    OutcomeReason: string option
    Dependencies: string list
    PhaseDependencies: string list
    Outputs: string list
    Artifacts: string
    Build: string
    BatchMode: string
    BatchId: string option
    Action: string option
    ActionReason: string option
    ActionDependencies: string list
    Cache: DiagnosticsTelemetry.CacheEvidence option
    Required: bool option
    RequirementReason: string option
    RequiredBy: string list
    EvaluationInputs: GraphDef.EvaluationInput list
    EnvironmentSensitiveInputs: GraphDef.EvaluationInput list
    EnvironmentSensitive: bool option
    EnvironmentSensitivityStatus: string
    ResolvedOperations: ResolvedOperationReport list
    Fingerprint: TargetFingerprint option
}

type BatchReport = {
    Id: string
    Members: string list
    Dependencies: string list
    Phase: string option
    TargetHash: string
    OperationHashes: string list
}

type OperationReport = {
    MetaCommand: string
    Command: string
    ArgumentsHash: string
    Container: string option
    DurationMs: float
    ExitCode: int
    Log: string
}

type ExecutionReport = {
    Id: string
    Kind: string
    Events: DiagnosticsTelemetry.TaskEvent list
    DurationMs: float option
    Operations: OperationReport list
}

type SlowTaskReport = {
    Id: string
    Kind: string
    DurationMs: float
}

type ResultReport = {
    Id: string
    Request: string
    Status: string
    Message: string option
}

type FScriptFunctionReport = {
    Id: string
    Count: int64
    TotalMs: float
    AverageMs: float
}

type FScriptReport = {
    ScriptLoads: int64
    ScriptLoadMs: float
    ScriptCacheHits: int64
    Invocations: int64
    InvocationMs: float
    ScriptEvaluations: int64
    ScriptEvaluationMs: float
    ToFScriptConversions: int64
    ToFScriptConversionMs: float
    FromFScriptConversions: int64
    FromFScriptConversionMs: float
    MethodResolutions: int64
    MethodResolutionMs: float
    Functions: FScriptFunctionReport list
}

type RunReport = {
    Status: string
    Completeness: string
    Error: string option
    TerrabuildVersion: string
    Workspace: string
    Targets: string list
    Configuration: string option
    Environment: string option
    Engine: string
    Force: bool
    Retry: bool
    LocalOnly: bool
    MaxConcurrency: int
    VariableNames: string list
    StartedAt: DateTime
    EndedAt: DateTime
    DurationMs: float
}

type PerformanceReport = {
    Phases: DiagnosticsTelemetry.PhaseTiming list
    ConfigurationProjects: DiagnosticsTelemetry.ProjectTiming list
    SlowestPhases: DiagnosticsTelemetry.PhaseTiming list
    SlowestTasks: SlowTaskReport list
    CriticalChain: string list
    FScript: FScriptReport
}

type Report = {
    SchemaVersion: int
    Run: RunReport
    Projects: ProjectFingerprint list
    Nodes: NodeReport list
    Batches: BatchReport list
    Results: ResultReport list
    Executions: ExecutionReport list
    Performance: PerformanceReport
}

let private appendValues (builder: StringBuilder) label emptyValue values =
    let rendered =
        match values with
        | [] -> emptyValue
        | _ -> values |> String.concat ", "
    builder.AppendLine($"  {label}: {rendered}") |> ignore

let internal renderEnvironmentSensitivitySummary violations =
    let builder = StringBuilder()

    match violations with
    | [] ->
        builder.AppendLine("Environment sensitivity: no selected targets need attention.") |> ignore
    | violations ->
        let count = violations |> List.length
        let targetLabel = if count = 1 then "target" else "targets"
        let consumeLabel = if count = 1 then "consumes" else "consume"
        builder.AppendLine($"Environment sensitivity: {count} selected {targetLabel} {consumeLabel} environment-sensitive inputs without opting in.") |> ignore

        violations
        |> List.iter (fun (nodeId, inputs) ->
            let inputNames = inputs |> String.concat ", "
            builder.AppendLine($"  - {nodeId}: {inputNames}") |> ignore)

        builder.AppendLine("  Remove the environment-sensitive inputs or set environment_sensitive = true for intentionally environment-specific artifacts.") |> ignore

    builder.ToString().TrimEnd()

let renderExplanation (report: Report) =
    let builder = StringBuilder()
    let selectedNodes = report.Nodes |> List.filter _.Selected

    selectedNodes
    |> List.tryFind (fun node ->
        node.SelectionKind = Some "explicit-root"
        && node.Action = Some "exec"
        && node.Scheduled = Some false)
    |> Option.iter (fun node ->
        raiseBugError $"Explicitly selected root '{node.Id}' resolved to exec but was not scheduled")

    let sensitivityViolations =
        selectedNodes
        |> List.filter (fun node ->
            node.EnvironmentSensitivityStatus = "missing-opt-in"
            || node.EnvironmentSensitivityStatus = "declared-neutral")
        |> List.map (fun node ->
            node.Id,
            node.EnvironmentSensitiveInputs |> List.map _.Name)

    builder.AppendLine(renderEnvironmentSensitivitySummary sensitivityViolations) |> ignore

    if selectedNodes <> [] then
        builder.AppendLine() |> ignore

    selectedNodes
    |> List.iteri (fun index node ->
        if index > 0 then
            builder.AppendLine() |> ignore

        let project = node.ProjectName |> Option.defaultValue node.ProjectId
        builder.AppendLine($"{project}:{node.Target}") |> ignore
        builder.AppendLine($"  id: {node.Id}") |> ignore
        let selectionKind = node.SelectionKind |> Option.defaultValue "unresolved"
        builder.AppendLine($"  selection: {selectionKind}") |> ignore

        let action = node.Action |> Option.defaultValue "unresolved"
        let actionReason = node.ActionReason |> Option.map (fun reason -> $" ({reason})") |> Option.defaultValue ""
        builder.AppendLine($"  action: {action}{actionReason}") |> ignore

        let outcome = node.Outcome |> Option.defaultValue "unresolved"
        let outcomeReason = node.OutcomeReason |> Option.map (fun reason -> $" ({reason})") |> Option.defaultValue ""
        builder.AppendLine($"  outcome: {outcome}{outcomeReason}") |> ignore

        let required = node.Required |> Option.map (fun value -> if value then "yes" else "no") |> Option.defaultValue "unresolved"
        let requirementReason = node.RequirementReason |> Option.map (fun reason -> $" ({reason})") |> Option.defaultValue ""
        builder.AppendLine($"  required: {required}{requirementReason}") |> ignore

        appendValues builder "dependencies" "none" node.Dependencies
        appendValues builder "action dependencies" "none" node.ActionDependencies

        match node.Cache with
        | Some cache ->
            let origin = cache.Origin |> Option.map (fun value -> $", origin {value}") |> Option.defaultValue ""
            builder.AppendLine($"  cache: {cache.Lookup} in {cache.Scope}{origin}") |> ignore
        | None -> builder.AppendLine("  cache: not consulted") |> ignore

        match node.Fingerprint with
        | Some fingerprint -> builder.AppendLine($"  cache key: {fingerprint.CacheKey}") |> ignore
        | None -> ()

        if node.EvaluationInputs <> [] then
            builder.AppendLine("  evaluated inputs:") |> ignore
            node.EvaluationInputs
            |> List.iter (fun input -> builder.AppendLine($"    - {input.Name}: {input.ValueHash}") |> ignore)

        if node.EnvironmentSensitiveInputs <> [] then
            builder.AppendLine("  environment-sensitive inputs:") |> ignore
            node.EnvironmentSensitiveInputs
            |> List.iter (fun input -> builder.AppendLine($"    - {input.Name}") |> ignore)

        builder.AppendLine($"  environment sensitivity: {node.EnvironmentSensitivityStatus}") |> ignore

        if node.ResolvedOperations <> [] then
            builder.AppendLine("  resolved operations:") |> ignore
            node.ResolvedOperations
            |> List.iter (fun operation ->
                builder.AppendLine($"    - {operation.MetaCommand}") |> ignore
                builder.AppendLine($"      command: {operation.Command}") |> ignore
                builder.AppendLine($"      arguments hash: {operation.ArgumentsHash}") |> ignore
                operation.Container |> Option.iter (fun value -> builder.AppendLine($"      container: {value}") |> ignore)
                operation.Platform |> Option.iter (fun value -> builder.AppendLine($"      platform: {value}") |> ignore)
                operation.Cpus |> Option.iter (fun value -> builder.AppendLine($"      cpus: {value}") |> ignore)
                appendValues builder "    forwarded variables" "none" operation.ForwardedVariableNames
                operation.InjectedEnvironment
                |> List.map _.Name
                |> appendValues builder "    injected environment" "none"))

    builder.ToString().TrimEnd()

type Context = {
    Options: ConfigOptions.Options
    Configuration: Configuration.Workspace option
    FullGraph: GraphDef.Graph option
    SelectedGraph: GraphDef.Graph option
    ResolvedGraph: GraphDef.Graph option
    FinalGraph: GraphDef.Graph option
    Cache: Cache.ICache option
    Summary: Runner.Summary option
    Status: string
    Completeness: string
    Error: string option
}

let private roundMs (value: float) = Math.Round(value, 2)

let internal normalizeOperationArguments (options: ConfigOptions.Options) (arguments: string) =
    let normalizedPaths =
        [
        options.Workspace, "$WORKSPACE"
        options.HomeDir, "$TERRABUILD_HOME"
        options.TmpDir, "$TERRABUILD_TMP"
        options.SharedDir, "$TERRABUILD_SHARED"
        ]
        |> Seq.filter (fun (path, _) -> String.IsNullOrWhiteSpace(path) |> not)
        |> Seq.map (fun (path, alias) -> Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), alias)
        |> Seq.distinctBy fst
        |> Seq.sortByDescending (fst >> String.length)
        |> Seq.fold (fun (normalized: string) (path, alias) ->
            normalized
                .Replace(path, alias, StringComparison.Ordinal)
                .Replace(path.Replace('\\', '/'), alias, StringComparison.Ordinal)
                .Replace(path.Replace('/', '\\'), alias, StringComparison.Ordinal)) arguments

    let normalizedUser =
        Text.RegularExpressions.Regex.Replace(normalizedPaths, @"--user \d+:\d+\s*", "")

    Text.RegularExpressions.Regex.Replace(
        normalizedUser,
        @"(--name\s+terrabuild-\S+-)[0-9a-fA-F]{12}(?=\s|$)",
        fun matched -> matched.Groups[1].Value + "$INSTANCE")

let private projectFingerprintCache = ConcurrentDictionary<string, ProjectFingerprint>()

let private projectFingerprints (options: ConfigOptions.Options) (configuration: Configuration.Workspace option) =
    match configuration with
    | None -> []
    | Some configuration ->
        configuration.Projects
        |> Seq.map (fun (KeyValue(_, project)) ->
            let cacheKey = $"{options.Workspace}\n{project.Id}\n{project.Hash}"
            projectFingerprintCache.GetOrAdd(cacheKey, fun _ ->
                let files =
                    project.Files
                    |> Seq.sort
                    |> Seq.map (fun relativePath ->
                        let fullPath = Path.Combine(options.Workspace, project.Directory, relativePath)
                        {
                            FileFingerprint.Path = FS.combinePath project.Directory relativePath
                            Hash = Hash.sha256file fullPath
                        })
                    |> List.ofSeq
                let fullPaths = files |> List.map (fun file -> Path.Combine(options.Workspace, file.Path))
                let dependencies =
                    project.Dependencies
                    |> Seq.sort
                    |> Seq.map (fun dependencyId -> {
                        DependencyFingerprint.Id = dependencyId
                        Hash = configuration.Projects[dependencyId].Hash
                    })
                    |> List.ofSeq
                {
                    ProjectFingerprint.Id = project.Id
                    Name = project.Name
                    Directory = project.Directory
                    Files = files
                    FileContentHash = Hash.sha256files fullPaths
                    FileNameHash = project.Files |> Seq.sort |> Hash.sha256strings
                    Dependencies = dependencies
                    DependenciesHash = dependencies |> Seq.map (fun dependency -> dependency.Hash) |> Seq.sort |> Hash.sha256strings
                    ProjectHash = project.Hash
                }))
        |> Seq.sortBy (fun project -> project.Id)
        |> List.ofSeq

let private nodeReports
    (options: ConfigOptions.Options)
    (configuration: Configuration.Workspace option)
    (fullGraph: GraphDef.Graph option)
    (selectedGraph: GraphDef.Graph option)
    (resolvedGraph: GraphDef.Graph option)
    (finalGraph: GraphDef.Graph option)
    (telemetry: DiagnosticsTelemetry.Snapshot) =
    match fullGraph with
    | None -> []
    | Some fullGraph ->
        let actionMap = telemetry.Actions |> Seq.map (fun item -> item.NodeId, item) |> Map.ofSeq
        let requirementMap = telemetry.Requirements |> Seq.map (fun item -> item.NodeId, item) |> Map.ofSeq
        let memberToBatch =
            finalGraph
            |> Option.map (fun graph ->
                graph.Batches
                |> Seq.collect (fun (KeyValue(batchId, members)) -> members |> Seq.map (fun memberId -> memberId, batchId))
                |> Map.ofSeq)
            |> Option.defaultValue Map.empty

        fullGraph.Nodes
        |> Seq.map (fun (KeyValue(nodeId, sourceNode)) ->
            let selected = selectedGraph |> Option.exists (fun graph -> graph.Nodes |> Map.containsKey nodeId)
            let selectionKind =
                if not selected then None
                elif selectedGraph |> Option.exists (fun graph -> graph.RootNodes |> Set.contains nodeId) then Some "explicit-root"
                else Some "dependency"
            let resolvedNode = resolvedGraph |> Option.bind (fun graph -> graph.Nodes |> Map.tryFind nodeId)
            let finalNode = finalGraph |> Option.bind (fun graph -> graph.Nodes |> Map.tryFind nodeId)
            let effectiveNode = finalNode |> Option.orElse resolvedNode |> Option.defaultValue sourceNode
            let action = actionMap |> Map.tryFind nodeId
            let requirement = requirementMap |> Map.tryFind nodeId
            let finalRoot = finalGraph |> Option.exists (fun graph -> graph.RootNodes |> Set.contains nodeId)
            let scheduled = finalGraph |> Option.map (fun _ -> finalRoot || (finalNode |> Option.exists _.Required))
            let outcome =
                match action |> Option.map _.Action, scheduled with
                | Some "exec", Some true -> Some "execute"
                | Some "restore", Some true -> Some "restore"
                | Some "summary", Some true -> Some "summary"
                | Some _, Some false -> Some "skip"
                | _ -> None
            let outcomeReason =
                match scheduled with
                | Some true when finalRoot -> selectionKind |> Option.orElse (Some "scheduled-root")
                | Some true when memberToBatch |> Map.containsKey nodeId -> Some "batch-member"
                | Some true -> requirement |> Option.map _.Reason |> Option.orElse (Some "required")
                | Some false ->
                    match action |> Option.map _.Action with
                    | Some "restore" -> Some "cache-hit-not-required"
                    | Some "summary" -> Some "cached-failure-not-required"
                    | Some "ignore" -> Some "ignored"
                    | Some _ -> requirement |> Option.map _.Reason |> Option.orElse (Some "not-required")
                    | None -> None
                | None -> None
            let targetFingerprint =
                resolvedNode
                |> Option.map (fun node ->
                    let declaredTargetHash =
                        configuration
                        |> Option.bind (fun config -> config.Projects |> Map.tryFind node.ProjectId)
                        |> Option.bind (fun project -> project.Targets |> Map.tryFind node.Target)
                        |> Option.map (fun target -> target.Hash)
                    let dependencies =
                        node.Dependencies - node.PhaseDependencies
                        |> Seq.sort
                        |> Seq.map (fun dependencyId -> {
                            DependencyFingerprint.Id = dependencyId
                            Hash = resolvedGraph.Value.Nodes[dependencyId].TargetHash
                        })
                        |> List.ofSeq
                    {
                        TargetFingerprint.DeclaredTargetHash = declaredTargetHash
                        OperationHashes = node.Operations |> List.map (Json.Serialize >> Hash.sha256)
                        DependencyTargetHashes = dependencies
                        PhaseDependenciesExcludedFromHash = node.PhaseDependencies |> Seq.sort |> List.ofSeq
                        TargetHash = node.TargetHash
                        CacheKey = GraphDef.buildCacheKey node
                    })
            let resolvedOperations =
                effectiveNode.Operations
                |> List.map (fun operation -> {
                    ResolvedOperationReport.MetaCommand = operation.MetaCommand
                    Command = operation.Command
                    ArgumentsHash = operation.Arguments |> normalizeOperationArguments options |> Hash.sha256
                    Container = operation.Image
                    Platform = operation.Platform
                    Cpus = operation.Cpus
                    ForwardedVariableNames = operation.Variables |> Seq.sort |> List.ofSeq
                    InjectedEnvironment =
                        operation.Envs
                        |> Seq.map (fun (KeyValue(name, value)) -> {
                            EnvironmentEntryReport.Name = name
                            ValueHash = value |> Hash.sha256
                        })
                        |> Seq.sortBy _.Name
                        |> List.ofSeq
                })
            {
                NodeReport.Id = nodeId
                ProjectId = sourceNode.ProjectId
                ProjectName = sourceNode.ProjectName
                ProjectDirectory = sourceNode.ProjectDir
                Target = sourceNode.Target
                Phase = sourceNode.Phase
                Selected = selected
                SelectionKind = selectionKind
                Scheduled = scheduled
                Outcome = outcome
                OutcomeReason = outcomeReason
                Dependencies = effectiveNode.Dependencies |> Seq.sort |> List.ofSeq
                PhaseDependencies = effectiveNode.PhaseDependencies |> Seq.sort |> List.ofSeq
                Outputs = effectiveNode.Outputs |> Seq.sort |> List.ofSeq
                Artifacts = (string effectiveNode.Artifacts).ToLowerInvariant()
                Build = (string effectiveNode.Build).ToLowerInvariant()
                BatchMode = (string effectiveNode.Batch).ToLowerInvariant()
                BatchId = memberToBatch |> Map.tryFind nodeId
                Action = action |> Option.map (fun item -> item.Action)
                ActionReason = action |> Option.map (fun item -> item.Reason)
                ActionDependencies = action |> Option.map (fun item -> item.Dependencies) |> Option.defaultValue []
                Cache = action |> Option.bind (fun item -> item.Cache)
                Required = requirement |> Option.map (fun item -> item.Required)
                RequirementReason = requirement |> Option.map (fun item -> item.Reason)
                RequiredBy = requirement |> Option.map (fun item -> item.Dependents) |> Option.defaultValue []
                EvaluationInputs = effectiveNode.EvaluationInputs
                EnvironmentSensitiveInputs = effectiveNode.EvaluationInputs |> GraphDef.environmentSensitiveInputs
                EnvironmentSensitive = effectiveNode.EnvironmentSensitive
                EnvironmentSensitivityStatus = GraphDef.environmentSensitivityStatus effectiveNode.EnvironmentSensitive effectiveNode.EvaluationInputs
                ResolvedOperations = resolvedOperations
                Fingerprint = targetFingerprint
            })
        |> Seq.sortBy (fun node -> node.Id)
        |> List.ofSeq

let private batchReports (graph: GraphDef.Graph option) =
    match graph with
    | None -> []
    | Some graph ->
        graph.Batches
        |> Seq.map (fun (KeyValue(batchId, members)) ->
            let node = graph.Nodes[batchId]
            {
                BatchReport.Id = batchId
                Members = members |> Seq.sort |> List.ofSeq
                Dependencies = node.Dependencies |> Seq.sort |> List.ofSeq
                Phase = node.Phase
                TargetHash = node.TargetHash
                OperationHashes = node.Operations |> List.map (Json.Serialize >> Hash.sha256)
            })
        |> Seq.sortBy (fun batch -> batch.Id)
        |> List.ofSeq

let private results (summary: Runner.Summary option) =
    match summary with
    | None -> []
    | Some summary ->
        summary.Nodes
        |> Seq.map (fun (KeyValue(nodeId, info)) ->
            let status, message =
                match info.Status with
                | Runner.TaskStatus.Success _ -> "success", None
                | Runner.TaskStatus.Failure (_, message) -> "failure", Some message
            {
                ResultReport.Id = nodeId
                Request = (string info.Request).ToLowerInvariant()
                Status = status
                Message = message
            })
        |> Seq.sortBy (fun result -> result.Id)
        |> List.ofSeq

let private eventDuration (events: DiagnosticsTelemetry.TaskEvent list) =
    let started =
        events
        |> List.tryFind (fun event -> event.Event.EndsWith("-started", StringComparison.Ordinal))
    let completed =
        events
        |> List.filter (fun event ->
            event.Event.EndsWith("-ended", StringComparison.Ordinal)
            || event.Event.EndsWith("-failed", StringComparison.Ordinal))
        |> List.tryLast
    match started, completed with
    | Some started, Some completed when completed.OffsetMs >= started.OffsetMs ->
        Some (roundMs (completed.OffsetMs - started.OffsetMs))
    | _ -> None

let private executionReports
    (options: ConfigOptions.Options)
    (cache: Cache.ICache option)
    (graph: GraphDef.Graph option)
    (telemetry: DiagnosticsTelemetry.Snapshot) =
    let scheduledIds =
        telemetry.TaskEvents
        |> Seq.filter (fun event -> event.Event = "scheduled")
        |> Seq.map (fun event -> event.TaskId)
        |> Set.ofSeq
    telemetry.TaskEvents
    |> Seq.filter (fun event -> scheduledIds |> Set.contains event.TaskId)
    |> Seq.groupBy (fun event -> event.TaskId)
    |> Seq.map (fun (taskId, events) ->
        let events =
            events
            |> Seq.sortBy (fun event -> event.OffsetMs, event.Event)
            |> Seq.map (fun event -> { event with OffsetMs = roundMs event.OffsetMs })
            |> List.ofSeq
        let kind =
            if graph |> Option.exists (fun graph -> graph.Batches |> Map.containsKey taskId) then "batch"
            elif events |> List.exists (fun event -> event.Event.StartsWith("restore")) then "restore"
            elif events |> List.exists (fun event -> event.Event.StartsWith("summary")) then "summary"
            else "execution"
        let operations =
            match cache, graph with
            | Some cache, Some graph ->
                let summaryNode =
                    match graph.Batches |> Map.tryFind taskId with
                    | Some members ->
                        members
                        |> Seq.choose (fun memberId -> graph.Nodes |> Map.tryFind memberId)
                        |> Seq.sortBy (fun node -> (if node.Action = GraphDef.RunAction.Exec then 0 else 1), node.Id)
                        |> Seq.tryHead
                    | None -> graph.Nodes |> Map.tryFind taskId
                match summaryNode with
                | Some node ->
                    let useRemote = GraphDef.isRemoteCacheable options node
                    match cache.TryGetSummaryOnly useRemote (GraphDef.buildCacheKey node) with
                    | Some (_, summary) ->
                        summary.Operations
                        |> List.collect id
                        |> List.map (fun (operation: Cache.OperationSummary) -> {
                            OperationReport.MetaCommand = operation.MetaCommand
                            Command = operation.Command
                            ArgumentsHash = operation.Arguments |> normalizeOperationArguments options |> Hash.sha256
                            Container = operation.Container
                            DurationMs = roundMs operation.Duration.TotalMilliseconds
                            ExitCode = operation.ExitCode
                            Log = operation.Log
                        })
                    | None -> []
                | None -> []
            | _ -> []
        {
            ExecutionReport.Id = taskId
            Kind = kind
            Events = events
            DurationMs = eventDuration events
            Operations = operations
        })
    |> Seq.sortBy (fun execution -> execution.Id)
    |> List.ofSeq

let private criticalChain (graph: GraphDef.Graph option) (executions: ExecutionReport list) =
    match graph with
    | None -> []
    | Some graph ->
        let byId = executions |> Seq.map (fun execution -> execution.Id, execution) |> Map.ofSeq
        let memberToBatch =
            graph.Batches
            |> Seq.collect (fun (KeyValue(batchId, members)) -> members |> Seq.map (fun memberId -> memberId, batchId))
            |> Map.ofSeq
        let execId nodeId = memberToBatch |> Map.tryFind nodeId |> Option.defaultValue nodeId
        let lastOffset (execution: ExecutionReport) = execution.Events |> List.last |> fun event -> event.OffsetMs
        let readyOffset (execution: ExecutionReport) =
            execution.Events
            |> List.tryFind (fun event -> event.Event = "ready")
            |> Option.orElseWith (fun () -> execution.Events |> List.tryHead)
            |> Option.map (fun event -> event.OffsetMs)
            |> Option.defaultValue 0.0
        let dependencyIds taskId =
            match graph.Batches |> Map.tryFind taskId with
            | Some members ->
                members
                |> Seq.collect (fun memberId ->
                    graph.Nodes
                    |> Map.tryFind memberId
                    |> Option.map (fun node -> node.Dependencies :> seq<string>)
                    |> Option.defaultValue Seq.empty)
                |> Seq.filter (fun dependencyId -> members |> Set.contains dependencyId |> not)
            | None ->
                graph.Nodes
                |> Map.tryFind taskId
                |> Option.map (fun node -> node.Dependencies :> seq<string>)
                |> Option.defaultValue Seq.empty
        let dependencies taskId =
            dependencyIds taskId
            |> Seq.map execId
            |> Seq.filter ((<>) taskId)
            |> Seq.distinct
            |> Seq.choose (fun dependencyId -> byId |> Map.tryFind dependencyId)
            |> Seq.filter (fun dependency -> lastOffset dependency <= readyOffset byId[taskId])
            |> List.ofSeq
        match executions |> List.filter (fun execution -> execution.Events <> []) |> List.sortByDescending (fun execution -> lastOffset execution, execution.Id) with
        | [] -> []
        | terminal :: _ ->
            let rec walk visited (execution: ExecutionReport) =
                if visited |> Set.contains execution.Id then []
                else
                    let previous =
                        dependencies execution.Id
                        |> List.sortByDescending (fun dependency -> lastOffset dependency, dependency.Id)
                        |> List.tryHead
                    match previous with
                    | Some previous -> walk (visited |> Set.add execution.Id) previous @ [ execution.Id ]
                    | None -> [ execution.Id ]
            walk Set.empty terminal

let private normalizeFunctionId (options: ConfigOptions.Options) (functionId: string) =
    let embeddedMarker = "__terrabuild_embedded__"
    let embeddedIndex = functionId.IndexOf(embeddedMarker, StringComparison.Ordinal)
    if embeddedIndex >= 0 then
        functionId.Substring(embeddedIndex).Replace('\\', '/')
    elif Path.IsPathRooted(functionId) && functionId.StartsWith(options.Workspace, StringComparison.Ordinal) then
        FS.relativePath options.Workspace functionId
    else
        functionId

let private fscriptReport options =
    let snapshot = Terrabuild.Scripting.getPerformanceSnapshot()
    {
        FScriptReport.ScriptLoads = snapshot.ScriptLoadCount
        ScriptLoadMs = roundMs snapshot.ScriptLoadDurationMs
        ScriptCacheHits = snapshot.ScriptCacheHitCount
        Invocations = snapshot.RuntimeInvokeCount
        InvocationMs = roundMs snapshot.RuntimeInvokeDurationMs
        ScriptEvaluations = snapshot.ScriptInvokeCount
        ScriptEvaluationMs = roundMs snapshot.ScriptInvokeDurationMs
        ToFScriptConversions = snapshot.ToFScriptConversionCount
        ToFScriptConversionMs = roundMs snapshot.ToFScriptConversionDurationMs
        FromFScriptConversions = snapshot.FromFScriptConversionCount
        FromFScriptConversionMs = roundMs snapshot.FromFScriptConversionDurationMs
        MethodResolutions = snapshot.MethodResolutionCount
        MethodResolutionMs = roundMs snapshot.MethodResolutionDurationMs
        Functions =
            snapshot.ScriptFunctionBreakdown
            |> List.map (fun (functionId, count, totalMs) -> {
                FScriptFunctionReport.Id = normalizeFunctionId options functionId
                Count = count
                TotalMs = roundMs totalMs
                AverageMs = if count = 0L then 0.0 else roundMs (totalMs / float count)
            })
            |> List.sortBy (fun functionReport -> functionReport.Id)
    }

let build (context: Context) =
    let telemetry = DiagnosticsTelemetry.snapshot()
    let executions = executionReports context.Options context.Cache context.FinalGraph telemetry
    let phases = telemetry.Phases |> List.map (fun phase -> { phase with DurationMs = roundMs phase.DurationMs; StartedOffsetMs = roundMs phase.StartedOffsetMs })
    let projects = telemetry.Projects |> List.map (fun project -> { project with DurationMs = roundMs project.DurationMs })
    let endedAt = DateTime.UtcNow
    {
        Report.SchemaVersion = 4
        Run = {
            RunReport.Status = context.Status
            Completeness = context.Completeness
            Error = context.Error
            TerrabuildVersion = Version.informalVersion()
            Workspace = context.Options.Workspace
            Targets = context.Options.Targets |> Seq.sort |> List.ofSeq
            Configuration = context.Options.Configuration
            Environment = context.Options.Environment
            Engine = (string context.Options.Engine).ToLowerInvariant()
            Force = context.Options.Force
            Retry = context.Options.Retry
            LocalOnly = context.Options.LocalOnly
            MaxConcurrency = context.Options.MaxConcurrency
            VariableNames = context.Options.Variables |> Map.keys |> Seq.sort |> List.ofSeq
            StartedAt = context.Options.StartedAt
            EndedAt = endedAt
            DurationMs = roundMs (endedAt - context.Options.StartedAt).TotalMilliseconds
        }
        Projects = projectFingerprints context.Options context.Configuration
        Nodes = nodeReports context.Options context.Configuration context.FullGraph context.SelectedGraph context.ResolvedGraph context.FinalGraph telemetry
        Batches = batchReports context.FinalGraph
        Results = results context.Summary
        Executions = executions
        Performance = {
            PerformanceReport.Phases = phases
            ConfigurationProjects = projects
            SlowestPhases = phases |> List.sortByDescending (fun phase -> phase.DurationMs, phase.Name) |> List.truncate 10
            SlowestTasks =
                executions
                |> List.choose (fun execution ->
                    execution.DurationMs
                    |> Option.map (fun duration -> {
                        SlowTaskReport.Id = execution.Id
                        Kind = execution.Kind
                        DurationMs = duration
                    }))
                |> List.sortByDescending (fun execution -> execution.DurationMs, execution.Id)
                |> List.truncate 20
            CriticalChain = criticalChain context.FinalGraph executions
            FScript = fscriptReport context.Options
        }
    }

let write filename context =
    build context
    |> Json.Serialize
    |> IO.writeTextFile filename
