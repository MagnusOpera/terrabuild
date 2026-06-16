---
title: serve
---

`terrabuild serve` starts serveable projects from the current workspace selection.

```text
USAGE: terrabuild serve [--help] [--workspace <path>] [--configuration <name>]
                        [--environment <name>] [--variable <variable>=<value>]
                        [--label [<labels>...]] [--type [<types>...]]
                        [--project [<projects>...]]
```

## Examples

```bash
terrabuild serve
terrabuild serve --project web
terrabuild serve --environment dev
```

Use filters like `--project`, `--label`, or `--type` to narrow which projects are served.
