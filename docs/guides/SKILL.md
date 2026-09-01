---
name: terrabuild
description: "Operate and configure Terrabuild workspaces: run or explain targets, choose scheduling and artifact policies, diagnose cache, phase, batch, lock, environment, reporting, and performance behavior, and recover from failed runs. Use for Terrabuild workspace work rather than developing Terrabuild itself."
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

## Operating Workflow

Find the nearest parent `WORKSPACE` and inspect its matching workspace target
before changing a project target. Shared scheduling policy normally belongs in
`WORKSPACE`; `PROJECT` supplies commands and project-specific exceptions.

For an existing workspace, prefer this sequence:

1. Run `terrabuild explain` with the same targets, filters, variables,
   configuration, environment, engine, and cache flags as the reported run.
2. Inspect `WORKSPACE`, the selected `PROJECT` files, and only the extension
   definitions involved in the decision.
3. Use a real `--debug --log` run only when command execution, restore behavior,
   reporting, or timing must be observed.
4. Do not add `--force`, clear caches, reduce parallelism, or disable batching
   merely to make a symptom disappear. Those actions change the workload; use
   them only when the user requests that experiment or the diagnosis calls for it.

## Choose Target Policy by Meaning

Commands define what runs. Target attributes independently define graph and
reuse semantics. Evaluate them in this order:

| Question | Attribute | Key distinction |
|---|---|---|
| Which concrete producers must finish first? | `depends_on` | Use target/project dependencies for producer-consumer relationships. |
| Must a workspace-wide class of work finish first? | `phase` | A prerequisite phase is a success barrier, not an ordinary hash dependency. |
| When should work execute and propagate? | `build` | `~auto`, `~always`, and `~lazy` have different propagation semantics. |
| Which files form the result? | `outputs` | Restores may replace the complete declared set. |
| Who owns the reusable artifact? | `artifacts` | Terrabuild, Insights, an external registry/service, or nobody. |
| Can compatible tasks share a native invocation? | `batch` | `~single`, `~partition`, or `~never`. |
| May contextual values affect identity? | `environment_sensitive` | Opt in only when environment, branch, tag, or CI state intentionally affects output. |
| Does work mutate machine-global state? | `lock` | Named locks serialize that resource across Terrabuild processes. |

Build modes:

- `~auto`: execute on cache miss or propagated execution from a non-lazy
  prerequisite; otherwise reuse the matching result.
- `~always`: execute whenever selected. Use for side effects or mutable external
  state, commonly with `artifacts = ~none`.
- `~lazy`: prepare work only for executing consumers and do not propagate its
  execution into otherwise reusable dependents. An explicitly selected lazy
  target still executes on its own cache miss.

Artifact modes:

- `~none`: retain neither a reusable summary nor restorable files.
- `~workspace`: retain summary, logs, and declared outputs locally.
- `~managed`: retain them locally and share encrypted outputs through Insights
  when connected.
- `~external`: retain the execution summary only. The image, package, or other
  artifact remains in its registry or service and Terrabuild never restores it.

`build` and `artifacts` answer different questions. For example, an external
Docker image can use `build = ~auto` and reuse its successful summary, while a
deployment can use `build = ~always` and `artifacts = ~none`.

When output or artifact defaults are inferred from several commands, the last
command is authoritative. Set `outputs` or `artifacts` explicitly when inference
would obscure ownership.

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
forwarded variable names and one aggregate value fingerprint are recorded without
their plaintext values. Follow
`executions[].operations[].log` only when command output is needed.

Keep planned and realized state separate:

- `.nodes[].action` and `actionReason` explain the prepared scheduling decision.
- `.results[].request` distinguishes `exec`, file `restore`, and cached-failure
  `summary` requests.
- `.results[].status` distinguishes `success`, attempted `failure`, and
  dependency `blocked` outcomes. A blocked task did not execute; inspect its
  direct blocker IDs.
- `.batches` describes synthetic physical execution. Logical member results
  remain separate and may reuse cache even when another member executes.

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

Execution in an ordinary non-lazy dependency propagates. Execution in a
prerequisite phase only satisfies its barrier and does not force a downstream
target with a valid cache entry to rebuild. Use `build = ~lazy` when a generator
or setup target must be available to executing consumers without invalidating
reusable downstream results.

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

Do not treat `blocked` results as additional command failures. Start from the
attempted `failure`, then follow blocker IDs to understand the unscheduled
dependents. A failed cached summary appears as a `summary` request unless
`--retry` deliberately replaces it with execution.

For an environment-sensitivity preparation failure, rerun `terrabuild explain`
with the same options; explanation mode reports the selected violations without
enforcing them. For other preparation failures, inspect the partial debug report:
the populated sections are checkpoints, while `.run.error` and
`terrabuild-debug.log` retain the stopping error. `explain` may itself stop before
producing readable output when the underlying preparation error is unrelated to
environment sensitivity.

### Did diagnostics finish cleanly?

Check `.run.status`, `.run.completeness`, and `.run.error` before drawing conclusions. With `completeness = "partial"`, use the populated sections as checkpoints and consult `terrabuild-debug.log` for the stopping error.

## Cache and Restore Semantics

For each target, distinguish three output states:

- not managed: Terrabuild owns no files;
- empty: the cached result intentionally contains no matching files;
- stored: a restorable archive exists.

Restoring an empty managed/workspace result removes stale files matching the
declared outputs. Stored output replacement is transactional: interrupted
application is rolled back before later configuration reading or project
hashing. If a successful summary requires stored outputs but the archive is
missing or unreadable, Terrabuild chooses execution with
`cache-outputs-missing` rather than reporting an impossible restore.

Remote artifact lookup and transfer are cache concerns: missing, corrupt, or
unreadable blobs become misses, and a failed upload does not discard the
completed local entry. This is separate from Insights reporting; see the strict
connection policy below.

Cache maintenance:

```bash
terrabuild prune 14
terrabuild clear --all
```

Use `prune` for stale local build entries and abandoned staging directories. Use
`clear --all` instead of manually deleting `~/.terrabuild`: it refuses to run
while another Terrabuild process is active and safely removes idle cache,
temporary, home, and lock state. Workspace restore journals remain beside their
workspace until recovery.

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

Phase targets are ordinary targets and may produce reusable artifacts. A
toolchain phase can therefore generate files or publish an external image that
later phase subgraphs consume. The barrier controls selection, ordering, and
success; artifact ownership and restoration still follow each target's
`artifacts` and `outputs` policy.

A common toolchain policy is:

```hcl
target generate {
  phase = phase.toolchains
  build = ~lazy
  artifacts = ~managed
  lock = "nuget-tools"
}
```

Inside target expressions, `terrabuild.phase` contains the assigned phase name. It evaluates to `nothing` for an unphased target.

## Batch Execution

Batching is possible only when resolved commands support it and nodes have
compatible extension cluster identities, target names, scripts, CPU limits, and
phase assignment.

- `~single` allows one native invocation across all compatible required tasks.
- `~partition` separates disconnected dependency components to reduce batch size
  and failure scope.
- `~never` executes compatible tasks individually; it does not make them
  sequential.

Terrabuild does not create a batch when no member needs execution, only one
compatible member remains, commands or policies differ, phases differ, or batch
contraction would create an external dependency cycle. A physical batch is
finalized only after member output staging and cache/Insights publication finish.
Use `.batches[].members` and the corresponding `.executions[]` entry for timing.

## Named Target Locks

Use a named lock when independent targets must not execute concurrently because they mutate the same machine-global resource:

```hcl
target gen {
  lock = "nuget-tools"
}
```

The matching workspace target provides the default lock for project targets. A project target may replace it or use `lock = nothing` to opt out. Executing nodes, batches, and cache hits that restore declared files acquire the lock; summary-only external cache hits do not. Operations sharing a name are serialized across both threads and Terrabuild processes through an exclusive file lease under the user-global Terrabuild profile. The lease is released by the operating system if the owning process terminates.

Long legitimate contention is reported periodically. Filesystem permission or
device errors fail immediately rather than being mistaken for contention. Use
`depends_on` instead when one task consumes another's output; a named lock only
protects shared mutable state.

## Insights Connection Policy

Insights is optional to configure, but connection behavior is binary:

- Without matching workspace credentials, Terrabuild creates no Insights client
  and uses only local cache/reporting.
- After `Connected to Insights`, build start, graph upload, artifact metadata,
  and completion reporting are mandatory. Failure in that lifecycle fails the
  Terrabuild command rather than silently leaving an incomplete Insights run.

Use `--local-only` when a run must neither use the managed cache nor report to
Insights. Do not interpret a successful local execution summary inside a partial
diagnostic as overall success when mandatory reporting subsequently failed.
