---
title: impact
---

`terrabuild impact` compares the current local graph with a graph previously stored for a base commit in Insights.

The compared graph is the selected source graph after configuration and target selection, before extension command resolution, action assignment, cascading, batching, or command execution. This keeps impact focused on graph and configuration hash changes, not on the current cache state.

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

## Output File

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

Impact states are:

- `changed`: the target hash differs from the base graph, or the node is missing from one side
- `dependency`: the node itself is unchanged, but it depends on a changed node

## Requirements

- You must be connected to Insights with `terrabuild login`.
- The base commit graph must already exist in Insights.
- Only named projects are included in the public output.
