---
title: run
---

`terrabuild run` is the main command used to build one or more targets.

```text
USAGE: terrabuild run [--help] [--workspace <path>] [--out <path>]
                      [--configuration <name>] [--environment <name>]
                      [--variable <variable>=<value>] [--label [<labels>...]]
                      [--type [<types>...]] [--project [<projects>...]]
                      [--force] [--retry] [--parallel <max>] [--local-only]
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

If `workspace.engine` is set in `WORKSPACE`, that workspace value overrides `--engine`.

## Machine-Readable Output

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

## Dry-Run Mode

Use `--dry-run` to prepare the run without executing it:

```bash
terrabuild run build --dry-run
```

In `--dry-run` mode Terrabuild does not invoke the runner, so no run result file is written.
