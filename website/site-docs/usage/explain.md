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

An explicit root that resolves to `exec` but is absent from the final schedule is
an internal consistency error. `explain` reports it as a Terrabuild bug instead
of presenting a contradictory `exec` and `not-required` combination.
