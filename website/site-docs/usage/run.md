---
title: run
---

`terrabuild run` executes one or more selected targets and their prerequisites.

```text
USAGE: terrabuild run [--help] [--workspace <path>] [--out <path>]
                      [--configuration <name>] [--environment <name>]
                      [--variable <variable>=<value>] [--label [<labels>...]]
                      [--type [<types>...]] [--project [<projects>...]]
                      [--force] [--retry] [--restore] [--parallel <max>] [--local-only]
                      [--note <note>] [--group <group>] [--tag <tag>]
                      [--engine <engine>]
                      [--dry-run] <target>...
```

## Examples

```bash
terrabuild run build
terrabuild run build test --parallel 4
terrabuild run build --environment dev --force
terrabuild run build --project api web
terrabuild run build --out run-result.json
terrabuild run deploy --group deployment-123
```

## Select targets and control execution

Pass one or more workspace target names, followed by any options. Use [shared selection options](./index.md#shared-selection-options) to choose the workspace, configuration, environment, variables, and projects. Required target dependencies are included automatically.

| Option | Purpose |
| --- | --- |
| `--force` (`-f`) | Ignore cached results and execute targets. |
| `--retry` (`-r`) | Retry failed cached tasks. |
| `--restore` | Materialize cached outputs of explicitly selected targets; see below. |
| `--parallel <max>` | Limit build concurrency; defaults to the number of processors. |
| `--local-only` | Use local cache only. |
| `--engine <engine>` | Choose `docker`, `podman`, or `host`. A configured `workspace.engine` overrides this option. |
| `--note <note>` | Attach a note to the build. |
| `--group <group>` | Associate related builds with a group identifier. |
| `--tag <tag>` | Attach a tag to the build. |
| `--out <path>` | Write the run result as JSON; see below. |
| `--dry-run` | Prepare the run without executing targets. |

Use [explain](./explain) for a readable account of the planned decisions, [logs](./logs) to inspect stored logs, and [compare](./compare) to investigate cache-key changes between debug runs.

## Machine-readable output

Use `--out` to write a JSON result file:

```bash
terrabuild run build --out run-result.json
```

The generated file contains:

- `status`: overall run status
- `targets`: requested targets
- `startedAt` and `endedAt`
- `results`: flat object keyed by `project:target`

Example queries:

```bash
jq -r '.status' run-result.json
jq -r '.results["app:build"]' run-result.json
jq -r '.results | to_entries[] | select(.value=="failure") | .key' run-result.json
```

Node states are:

- `success`
- `failure`
- `ignored`

`ignored` means the node exists in the final graph but the runner did not execute or restore it during that run.

## Dry-run mode

Use `--dry-run` to prepare the run without executing it:

```bash
terrabuild run build --dry-run
```

In `--dry-run` mode Terrabuild does not invoke the runner, so no run result file is written.
## Restore selected cached outputs

Use `--restore` when a later CI step needs files from a cached target:

```bash
terrabuild run test --project api_tests web_tests --restore
```

Cached managed or workspace targets selected by the requested target names and project filters remain restoration roots. Their declared outputs, such as TRX and JUnit reports, are materialized without rerunning successful checks. Explicitly requesting several targets restores each selected cached target, even when one is also another target's dependency. Cached roots remain dependency barriers, so their build-time prerequisites are not restored merely to collect reports. External artifacts are not downloaded into the checkout.

Cache misses and failures retain normal `run` semantics, including `--retry`, and `--dry-run` only previews the graph. `--restore` changes output materialization, not cache keys. File replacement uses the same transactional restoration and target locks as ordinary dependency restoration. Without this option, existing lazy restoration behavior is unchanged.
