---
title: clear
---

`terrabuild clear` removes local Terrabuild cache data.

Clearing is profile-wide and refuses to start while another Terrabuild process is active. This protects running commands from losing cache, toolchain, temporary, restore-journal, or lock state midway through execution. Retry the clear after those commands finish; abandoned process leases are removed automatically.

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
- `--all`: clear every local cache and remove idle target and cache-restore lock files. A lock currently held by another Terrabuild process is left in place and remains valid until that process releases it. Interrupted output transaction data is retained beside its workspace until the next restore uses it for recovery.
