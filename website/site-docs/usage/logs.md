---
title: logs
---

`terrabuild logs` loads logs for selected targets without running a new build.

```text
USAGE: terrabuild logs [--help] [--workspace <path>] [--configuration <name>]
                       [--environment <name>] [--variable <variable>=<value>]
                       [--label [<labels>...]] [--type [<types>...]]
                       [--project [<projects>...]] [--local-only] <target>...
```

## Examples

```bash
terrabuild logs build
terrabuild logs build --project api
terrabuild logs build --environment dev --local-only
```

Use this command when you want to inspect the cached result for a target instead of executing `terrabuild run`.

Use [shared selection options](./index.md#shared-selection-options) to match the configuration, environment, variables, and projects of the build whose logs you need. `--local-only` requests local cache only.
