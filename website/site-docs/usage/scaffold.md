---
title: scaffold
---

`terrabuild scaffold` creates starter configuration files for a workspace.

```text
USAGE: terrabuild scaffold [--help] [--workspace <path>] [--force]
```

## Examples

```bash
terrabuild scaffold
terrabuild scaffold --workspace ./my-repo
terrabuild scaffold --force
```

By default, existing `WORKSPACE` or `PROJECT` files are preserved. Use `--force` to overwrite them.
