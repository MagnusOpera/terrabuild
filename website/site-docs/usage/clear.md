---
title: clear
---

`terrabuild clear` removes local Terrabuild cache data.

```text
USAGE: terrabuild clear [--help] [--cache] [--home] [--temporary] [--all]
```

## Examples

```bash
terrabuild clear --cache
terrabuild clear --temporary
terrabuild clear --all
```

Options:

- `--cache`: clear build cache
- `--home`: clear the Terrabuild home cache
- `--temporary`: clear temporary files
- `--all`: clear every local cache and remove idle named-target lock files. A lock currently held by another Terrabuild process is left in place and remains valid until that process releases it.
