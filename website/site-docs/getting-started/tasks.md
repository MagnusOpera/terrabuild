---
title: Tasks

prev: /docs/getting-started/graph

---

The [build graph](/docs/getting-started/graph) is the plan; tasks are the units of work Terrabuild schedules to carry it out.

## What is a Task?

A **task** is one target applied to one project—for example, “run the `build` target for project X.” When you run `terrabuild run build`, Terrabuild selects the relevant project targets and represents each one as a task in the graph.

The target supplies the commands and behavior; the project supplies the files, dependencies, and variables. Together they give Terrabuild everything it needs to schedule the task correctly.

## From Selection to Completion

Every run follows the same broad sequence:

1. **Select** — Terrabuild starts from the requested target and discovers the tasks required by its dependencies and phases.
2. **Plan** — It validates the graph, orders dependent work, and identifies tasks that may run concurrently.
3. **Resolve** — When a task becomes ready, Terrabuild determines whether it must execute or whether an earlier result can be reused.
4. **Complete** — The task produces a result that unlocks its dependents; independent tasks continue in parallel.

Dependencies are readiness constraints, not a serial execution list. A task waits only for the work it depends on, so unrelated branches of the graph can progress at the same time.

## Task Outcomes

Scheduling tells Terrabuild **when** a task may proceed. Once ready, the task resolves to one of three outcomes:

| Action | Description |
|--------|-------------|
| `Build` | Execute the target commands |
| `Restore` | Restore a successful cache hit |
| `Summary` | Report a previous failed cached run without executing commands or restoring outputs |

The task graph treats each outcome as completion, but only a successful build or restore can satisfy work that requires successful outputs. If a task fails, its dependents do not run.

The next page, [Caching](/docs/getting-started/caching), explains how Terrabuild computes reusable identities and chooses between these outcomes.

## Propagating Work

If a task builds instead of restoring, dependent tasks are also marked for build unless that dependency uses `build = ~lazy`. This keeps downstream outputs consistent after real work occurs while allowing lazy setup targets to avoid unnecessary rebuild propagation.

Task scheduling also respects workspace-wide [phases](../workspace/phase) and may combine compatible tasks into [batch builds](/docs/getting-started/batch). Those features change how work is grouped or ordered; they do not change what a task represents.
