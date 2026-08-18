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

Select a target in **Node Details** to see the same decision evidence exposed by
`terrabuild explain`: action and requirement reasons, cache lookup, evaluated
inputs, and resolved operations. Potentially sensitive values remain hashed.

For the interface walkthrough, graph node legend, colors, dependency arrows, and phase display, see the dedicated [Console documentation](../console/).
