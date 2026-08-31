---
title: Tasks

prev: /docs/getting-started/graph
next: /docs/getting-started/target-policies

---

The [build graph](/docs/getting-started/graph) is the plan; tasks are the units of work Terrabuild schedules to carry it out.

## What is a task?

A **task** is one target applied to one project. For example, the `build` target for project X becomes one task. When you run `terrabuild run build`, Terrabuild selects the relevant project targets and represents each one as a task in the graph.

The target supplies the commands and behavior; the project supplies the files, dependencies, and variables. Together they give Terrabuild everything it needs to schedule the task correctly.

## From selection to completion

Every run follows the same broad sequence:

1. Terrabuild starts from the requested target and selects the tasks required by its dependencies and phases.
2. It validates the graph, orders dependent work, and identifies tasks that may run concurrently.
3. When a task becomes ready, Terrabuild decides whether to execute it or reuse an earlier result.
4. The result unlocks dependent tasks while independent work continues in parallel.

Dependencies are readiness constraints, not a serial execution list. A task waits only for the work it depends on, so unrelated branches of the graph can progress at the same time.

## Task outcomes

Scheduling tells Terrabuild **when** a task may proceed. Once ready, the task resolves to one of three outcomes:

| Action | Description |
|--------|-------------|
| `Build` | Execute the target commands |
| `Restore` | Reuse a successful cache hit. Terrabuild restores declared files for workspace or managed artifacts; an external artifact needs only its successful summary. |
| `Summary` | Report a previous failed cached run without executing commands or restoring outputs |

The task graph treats each outcome as completion, but only a successful build or cache hit can satisfy dependent work. External artifacts remain in their registry or service and are never copied into the workspace. If a task fails, its dependents do not run.

The next page, [Target policies](./target-policies), explains how target attributes control these scheduling and reuse decisions. [Caching](/docs/getting-started/caching) then describes reusable identities and storage in detail.

## Propagating work

If a task builds instead of restoring, dependent tasks are also marked for build unless that dependency uses `build = ~lazy`. This keeps downstream outputs consistent after real work occurs while allowing lazy setup targets to avoid unnecessary rebuild propagation.

Task scheduling also respects workspace-wide [phases](../workspace/phase) and may combine compatible tasks into [batch builds](/docs/getting-started/batch). Those features change how work is grouped or ordered; they do not change what a task represents.
