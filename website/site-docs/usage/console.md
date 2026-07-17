---
title: console
---

`terrabuild console` launches the local Terrabuild web console.

```text
USAGE: terrabuild console [--help] [--workspace <path>] [--no-open]
```

## Examples

```bash
terrabuild console
terrabuild console --no-open
terrabuild console --workspace ./repo
```

Use `--no-open` if you want to start the console without opening a browser automatically.

## Graph options

The console's **Advanced** panel controls how the execution graph is generated:

* **Debug** includes diagnostic graph details.
* **Phases** groups phased targets and displays phase ordering. It is disabled by default because grouping can make large graphs harder to read.

Changing either option recomputes the graph layout. Enable **Phases** when workspace-wide ordering is useful to inspect; leave it off for a compact dependency view.
