---
title: impact
---

`terrabuild impact` compares the current local graph with a graph previously stored for a base commit in Insights.

The comparison uses resolved logical targets, after extension command resolution and before cache-dependent action assignment, cascading, batching, or command execution. Both commits use the same target hash calculation. Synthetic batch nodes are excluded.

Use the branch point (the common ancestor of the branch and its target branch) as `--base` to measure branch impact. Terrabuild uses the SHA you provide; it does not discover that ancestor automatically. Run from a clean checkout of the branch HEAD, with equivalent configuration, environment, and variables at both commits. The current graph is built from local files.

```text
USAGE: terrabuild impact [--help] --base <sha> --out <path> [--workspace <path>]
                         [--configuration <name>] [--environment <name>]
                         [--variable <variable>=<value>] [--label [<labels>...]]
                         [--type [<types>...]] [--project [<projects>...]]
                         <target>...
```

## Example

```bash
terrabuild impact build --base 4d2f6d9 --out impact.json
```

## Output file

`impact` always writes its report to the file provided with `--out`.

The file contains:

- `base`: the compared base commit
- `head`: the current checked-out commit
- `targets`: requested targets
- `impacts`: flat object keyed by `project:target`

Example queries:

```bash
jq -r '.base' impact.json
jq -r '.impacts["app:build"]' impact.json
jq -r '.impacts | to_entries[] | select(.value=="changed") | .key' impact.json
```

Each entry has the state `changed`: the set of target hashes for that `project:target` differs between base and HEAD. This includes added and removed targets, including targets belonging to deleted projects. A requested target removed from `WORKSPACE` can still be compared using its baseline graph. Identical hash sets are omitted. Dependency changes are already reflected in the Merkle target hashes; there is no separate dependency traversal or `dependency` state.

## Requirements

- You must be connected to Insights with `terrabuild login`.
- The base commit graph must already exist in Insights, with resolved target hashes for the requested scope. Incomplete or legacy baselines produce an error rather than a speculative change report. Record a build of the requested targets at the base commit to supply a comparable baseline.
- The stored full graph is narrowed to requested targets and their dependency closure. Project filters use current project metadata; deleted projects are included as roots when no project filter is active.
- Only named projects are included in the public output.
