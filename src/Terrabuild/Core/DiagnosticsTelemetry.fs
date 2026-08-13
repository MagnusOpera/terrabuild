module DiagnosticsTelemetry

open System
open System.Collections.Concurrent
open System.Diagnostics

[<RequireQualifiedAccess>]
type CacheEvidence = {
    Scope: string
    Key: string
    Lookup: string
    Origin: string option
    PreviousStatus: string option
    SummaryEndedAt: DateTime option
}

[<RequireQualifiedAccess>]
type ActionDecision = {
    NodeId: string
    Action: string
    Reason: string
    Dependencies: string list
    Cache: CacheEvidence option
}

[<RequireQualifiedAccess>]
type RequirementDecision = {
    NodeId: string
    Required: bool
    Reason: string
    Dependents: string list
}

[<RequireQualifiedAccess>]
type PhaseTiming = {
    Name: string
    StartedOffsetMs: float
    DurationMs: float
}

[<RequireQualifiedAccess>]
type ProjectTiming = {
    ProjectId: string
    DurationMs: float
}

[<RequireQualifiedAccess>]
type TaskEvent = {
    TaskId: string
    Event: string
    OffsetMs: float
}

[<RequireQualifiedAccess>]
type Snapshot = {
    Actions: ActionDecision list
    Requirements: RequirementDecision list
    Phases: PhaseTiming list
    Projects: ProjectTiming list
    TaskEvents: TaskEvent list
}

let mutable private enabled = false
let mutable private startedAt = Stopwatch.GetTimestamp()
let private actions = ConcurrentDictionary<string, ActionDecision>()
let private requirements = ConcurrentDictionary<string, RequirementDecision>()
let private phases = ConcurrentBag<PhaseTiming>()
let private projects = ConcurrentBag<ProjectTiming>()
let private taskEvents = ConcurrentBag<TaskEvent>()

let private ticksToMs ticks =
    (float ticks * 1000.0) / float Stopwatch.Frequency

let offsetMs () =
    Stopwatch.GetTimestamp() - startedAt |> ticksToMs

let reset isEnabled =
    enabled <- isEnabled
    startedAt <- Stopwatch.GetTimestamp()
    actions.Clear()
    requirements.Clear()
    phases.Clear()
    projects.Clear()
    taskEvents.Clear()

let recordAction (decision: ActionDecision) =
    if enabled then actions[decision.NodeId] <- decision

let recordRequirement (decision: RequirementDecision) =
    if enabled then requirements[decision.NodeId] <- decision

let recordPhase name startedOffset duration =
    if enabled then
        phases.Add {
            PhaseTiming.Name = name
            StartedOffsetMs = startedOffset
            DurationMs = duration
        }

let recordProject projectId duration =
    if enabled then
        projects.Add {
            ProjectTiming.ProjectId = projectId
            DurationMs = duration
        }

let recordTask taskId eventName =
    if enabled then
        taskEvents.Add {
            TaskEvent.TaskId = taskId
            Event = eventName
            OffsetMs = offsetMs ()
        }

let snapshot () : Snapshot =
    {
        Actions = actions.Values |> Seq.sortBy (fun item -> item.NodeId) |> List.ofSeq
        Requirements = requirements.Values |> Seq.sortBy (fun item -> item.NodeId) |> List.ofSeq
        Phases = phases |> Seq.sortBy (fun item -> item.StartedOffsetMs, item.Name) |> List.ofSeq
        Projects = projects |> Seq.sortBy (fun item -> item.ProjectId) |> List.ofSeq
        TaskEvents = taskEvents |> Seq.sortBy (fun item -> item.OffsetMs, item.TaskId, item.Event) |> List.ofSeq
    }
