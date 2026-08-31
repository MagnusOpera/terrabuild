---
name: terrabuild
description: Use Terrabuild workspaces to run targets, inspect graph and cache decisions, troubleshoot failed or unexpectedly rebuilt targets, investigate build performance, and configure build phases. Use for operating Terrabuild in a workspace rather than developing Terrabuild itself.
---

# Terrabuild Usage Skill

This guide focuses on using Terrabuild as a workspace user.

## What Terrabuild Does

Terrabuild reads your `WORKSPACE`, computes the dependency graph, and executes targets (`install`, `build`, `test`, `dist`, etc.) in the right order with parallelism.

## Core Concepts

- `WORKSPACE`: top-level workspace definition.
- `target`: lifecycle stage to run (for example `build`, `test`).
- `phase`: an optional workspace-declared ordering boundary spanning multiple targets.
- project: a repository unit matched by workspace rules.
- extension: reusable command provider (for example `@dotnet`, `@pnpm`, `@docker`, `@terraform`).

## Daily Commands

Show help:

```bash
terrabuild --help
terrabuild run --help
terrabuild logs --help
```

Run one target:

```bash
terrabuild run build
```

Run multiple targets:

```bash
terrabuild run build test
```

Run with explicit context:

```bash
terrabuild run build --workspace . --configuration local --environment dev
```

Force rerun and keep execution logs:

```bash
terrabuild run build --force --log
```

Run locally only:

```bash
terrabuild run build --local-only
```

Tune parallelism:

```bash
terrabuild run build --parallel 4
```

Explain target selection and execution decisions without executing commands:

```bash
terrabuild explain build --project app
```

Use the same targets, filters, configuration, environment, variables, engine,
and cache flags as the run being investigated. The readable explanation shows
selected targets, dependencies, action and cache decisions, evaluated inputs,
resolved operations, and environment-sensitivity problems. It is generated from
the same diagnostic model as `terrabuild-debug.json` and keeps input values and
operation arguments hashed. Because `explain` does not execute commands, it
cannot diagnose command failures or provide execution timings.

Replay logs for targets:

```bash
terrabuild logs build test --log
```

## Useful Debug Mode

When execution is unclear, reproduce it with the original options plus debug
output and retained operation logs:

```bash
terrabuild run build --debug --log
```

Do not add `--force` by default: it changes cache and execution decisions and can
hide the behavior under investigation. Add it only when deliberately reproducing
an uncached execution, and keep that distinction explicit in performance
comparisons.

Debug mode replaces the previous diagnostic artifacts on every run and produces:

- `terrabuild-debug.json`: the canonical, versioned diagnostic report.
- `terrabuild-debug.log`: chronological commands, warnings, failures, and stack traces.

Read `terrabuild-debug.json` first. It is deterministic in ordering and may be
`partial` if preparation or execution stopped early. Each node records the
evaluated inputs that affected it and its resolved operations. Sensitive values,
injected environment values, and operation arguments are represented by hashes;
forwarded variable names are recorded without their values. Follow
`executions[].operations[].log` only when command output is needed.

Use `explain` before execution when the question concerns selection, dependency,
cache, input, resolved-operation, or environment-sensitivity decisions. Use a
debug run when the question concerns actual duration or command execution. Debug
mode replaces the previous diagnostic artifacts, so preserve a report before
running a different reproduction when the comparison matters.

## Investigating Common Issues

### Why did a target rebuild?

Locate the target in `.nodes[]` and inspect `action`, `actionReason`, `actionDependencies`, and `cache`:

```bash
jq '.nodes[] | select(.projectName == "app" and .target == "build") |
    {action, actionReason, actionDependencies, cache, fingerprint}' terrabuild-debug.json
```

Reason codes are:

- `forced-cli`: `--force` requested execution.
- `configured-always`: the target uses `build = ~always`.
- `dependency-executed`: one or more IDs in `actionDependencies` executed and propagated the rebuild.
- `non-cacheable`: the target does not retain a reusable summary.
- `cache-miss`: no summary exists for the reported cache key and scope.
- `cache-outputs-missing`: a successful managed or workspace summary exists, but its declared output archive cannot be restored, so Terrabuild executes the target again.
- `retry-failed-cache`: `--retry` replaced a failed cached result with execution.
- `cached-failure`: Terrabuild reported a previous failed result without rerunning it.
- `cache-hit`: Terrabuild reused a successful local or remote summary.

To determine which input changed, compare the node's `fingerprint` and its project's entry in `.projects` between two reports. Start with the aggregate hashes, then compare `files`, dependency hashes, declared target hash, and operation hashes. Phase dependencies are listed separately because they do not participate in the target hash.

### Where did the run spend time?

Collect diagnostics from an actual representative run; `explain` contains no
execution timings. Keep targets, filters, configuration, environment,
parallelism, cache state, and `--force` usage consistent when comparing runs.
Treat warm-cache and deliberately uncached measurements as different workloads.

Start with the ranked summaries and critical chain:

```bash
jq '.performance | {slowestPhases, slowestTasks, criticalChain, fScript}' terrabuild-debug.json
```

Then inspect the critical-chain entries in `.executions[]`. Their ordered events separate scheduling/queue time, execution or restore time, and upload time. A batch appears once in `.batches` and `.executions`; use its `members` rather than assigning its duration to each member.

### Why did a command fail?

Find failed logical results, join them to executions by ID (or through a node's `batchId`), and open the referenced operation log:

```bash
jq '.results[] | select(.status == "failure")' terrabuild-debug.json
jq '.executions[] | select(any(.operations[]; .exitCode != 0))' terrabuild-debug.json
```

Use `terrabuild-debug.log` for Terrabuild exceptions, infrastructure errors, or failures that occurred before an operation log was created.

For an environment-sensitivity preparation failure, rerun `terrabuild explain`
with the same options; explanation mode reports the selected violations without
enforcing them. For other preparation failures, inspect the partial debug report:
the populated sections are checkpoints, while `.run.error` and
`terrabuild-debug.log` retain the stopping error. `explain` may itself stop before
producing readable output when the underlying preparation error is unrelated to
environment sensitivity.

### Did diagnostics finish cleanly?

Check `.run.status`, `.run.completeness`, and `.run.error` before drawing conclusions. With `completeness = "partial"`, use the populated sections as checkpoints and consult `terrabuild-debug.log` for the stopping error.

## Workspace Usage Tips

- Keep target names consistent (`install`, `build`, `test`, `dist`).
- Use `depends_on` to model build order.
- Put common defaults in extension definitions.
- Use `locals` for environment-specific values.

## Optional Build Phases

Declare phases in `WORKSPACE` when a group of targets must finish before another group can start:

```hcl
phase toolchains {}

phase application {
  depends_on = [phase.toolchains]
}

target build {
  phase = phase.application
}
```

Both workspace and project targets can reference a phase, but `phase` blocks themselves are valid only in `WORKSPACE`. A project target inherits the phase on its matching workspace target. Use `phase = nothing` in the project target to opt out.

Selecting a phased target enlists all targets assigned to its transitive prerequisite phases, along with their normal dependencies. It does not enlist unrelated targets in its own phase. Targets without a phase keep normal dependency behavior.

Targets in one phase may run concurrently, but no target in a downstream phase starts until every enlisted prerequisite-phase target succeeds. Batch execution never combines targets from different phases or combines phased and unphased targets. Markdown graphs remain ungrouped; phase grouping is an opt-in view in the interactive console.

Inside target expressions, `terrabuild.phase` contains the assigned phase name. It evaluates to `nothing` for an unphased target.

## Named Target Locks

Use a named lock when independent targets must not execute concurrently because they mutate the same machine-global resource:

```hcl
target gen {
  lock = "nuget-tools"
}
```

The matching workspace target provides the default lock for project targets. A project target may replace it or use `lock = nothing` to opt out. Executing nodes, batches, and cache hits that restore declared files acquire the lock; summary-only external cache hits do not. Operations sharing a name are serialized across both threads and Terrabuild processes through an exclusive file lease under the user-global Terrabuild profile. The lease is released by the operating system if the owning process terminates.
