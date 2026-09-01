---
title: explain
---

`terrabuild explain` prepares the selected graph without executing it and prints
why each target would execute, restore, summarize, or be ignored. The readable
output is projected from the same canonical report used by `--debug`.

```text
USAGE: terrabuild explain [--help] [--workspace <path>]
                          [--configuration <name>] [--environment <name>]
                          [--variable <variable>=<value>] [--label [<labels>...]]
                          [--type [<types>...]] [--project [<projects>...]]
                          [--force] [--retry] [--local-only]
                          [--engine <engine>] <target>...
```

## Examples

```bash
terrabuild explain build
terrabuild explain build --project api
terrabuild explain deploy --environment staging
terrabuild explain build --force
```

For every selected node, the output distinguishes an explicit root from a
dependency, reports the computed action, and states the final scheduling outcome
with its reason. It also includes requirement, dependency, and cache evidence,
the cache key, evaluated input names, and resolved operation metadata. Values
that could contain secrets remain hashed.

For a completed debug run, the action remains the scheduler's original decision
while the outcome describes the logical disposition of that node. For example, if
cached outputs disappear between planning and restoration, the action remains
`restore` but the outcome becomes `execute (restore-missed)`. A cached member folded
into a native batch remains `restore (batch-cache-reuse)` because its existing
artifact is reused rather than republished; `batchId` links it to the physical batch
command shown under `batches` and `executions`.

An explicit root that resolves to `exec` but is absent from the final schedule is
an internal consistency error. `explain` reports it as a Terrabuild bug instead
of presenting a contradictory `exec` and `not-required` combination.
