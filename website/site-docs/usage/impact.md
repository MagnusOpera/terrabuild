---
title: impact
---

`terrabuild impact` compares the current local graph with a graph previously stored for a base commit in Insights.

Use it to produce a machine-readable list of changed targets for a branch. The current graph is built from local files, so run from a clean checkout of the branch HEAD to measure committed changes.

```text
USAGE: terrabuild impact [--help] --base <sha> --out <path> [--workspace <path>]
                         [--configuration <name>] [--environment <name>]
                         [--variable <variable>=<value>] [--label [<labels>...]]
                         [--type [<types>...]] [--project [<projects>...]]
                         <target>...
```

## Compare a branch with its base

First [connect to Insights](./login). The base commit must have a recorded build in the same Insights workspace and environment. If no comparable graph exists, run the requested targets at that commit and let Terrabuild upload the graph before returning to the branch being evaluated.

From a clean checkout of your branch, compute its common ancestor with the target branch and pass the resulting SHA:

```bash
base=$(git merge-base HEAD origin/main)
terrabuild impact build test --base "$base" --out impact.json
```

Replace `origin/main` with your target branch. Fetch that branch first if your local reference is out of date. Use the same configuration, environment, and variables as the baseline build:

```bash
terrabuild impact build --base "$base" --out impact.json \
  --configuration Release --environment staging --project api web
```

The command resolves target operations but does not execute build targets. A successful comparison returns exit code `0`, including when changes are found.

## Arguments and options

| Argument or option | Required | Purpose |
| --- | --- | --- |
| `<target>...` | Yes | One or more workspace target names to compare, including their dependency closure. |
| `--base <sha>` | Yes | Exact commit SHA whose graph is already stored in Insights. Git branch names and ancestor discovery are not resolved automatically. |
| `--out <path>` | Yes | Destination JSON file. Missing parent directories are created. |
| `--workspace <path>` (`-w`) | No | Workspace root; otherwise discovered from the current directory. |
| `--configuration <name>` (`-c`) | No | Configuration used to resolve the local graph. |
| `--environment <name>` (`-e`) | No | Environment used for local evaluation and baseline graph lookup. |
| `--variable <name>=<value>` (`-v`) | No | Set a variable; repeat for multiple variables. |
| `--project <projects>...` (`-p`) | No | Select projects by identifier or workspace-relative path. |
| `--label <labels>...` (`-l`) | No | Select projects matching any supplied label. |
| `--type <types>...` (`-t`) | No | Select projects matching any supplied extension type. |

See [shared selection options](./index.md#shared-selection-options) for filter combinations. Filters select roots; their dependencies are included in the comparison.

## Output file

`impact` always writes its report to the file provided with `--out`.

The file contains:

- `base`: the compared base commit
- `head`: the current checked-out commit
- `targets`: requested targets
- `impacts`: flat object keyed by `project:target`

For example:

```json
{
  "base": "4d2f6d9",
  "head": "8a7c1e3",
  "targets": ["build"],
  "impacts": {
    "api:build": "changed",
    "web:build": "changed"
  }
}
```

An empty `impacts` object means no target hash sets changed in the selected scope.

Example queries:

```bash
jq -r '.base' impact.json
jq -r '.impacts["app:build"]' impact.json
jq -r '.impacts | to_entries[] | select(.value=="changed") | .key' impact.json
```

## How changes are detected

Each entry has the state `changed`: the set of target hashes for that `project:target` differs between base and HEAD. This includes added and removed targets, including targets belonging to deleted projects. A requested target removed from `WORKSPACE` can still be compared using its baseline graph. Identical hash sets are omitted. Dependency changes are already reflected in the Merkle target hashes; there is no separate dependency traversal or `dependency` state.

The comparison uses resolved logical targets, after extension command resolution and before cache-dependent action assignment, cascading, batching, or command execution. Both commits use the same target hash calculation. Synthetic batch nodes are excluded.

## Requirements

- You must be connected to Insights with `terrabuild login`.
- The base commit graph must already exist in Insights, with resolved target hashes for the requested scope. Incomplete or legacy baselines produce an error rather than a speculative change report. Record a build of the requested targets at the base commit to supply a comparable baseline.
- The stored full graph is narrowed to requested targets and their dependency closure. Project filters use current project metadata; deleted projects are included as roots when no project filter is active.
- Only named projects are included in the public output.

To inspect current execution decisions, use [explain](./explain). To investigate which files or inputs changed a cache key between saved runs, use [compare](./compare).
