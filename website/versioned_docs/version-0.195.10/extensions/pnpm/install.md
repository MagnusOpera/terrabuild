---
title: "install"
---
Installs packages with `pnpm install`, optionally honoring the lockfile and batching across workspaces.
```
@pnpm install {
    force = false
    frozen = true
    args = "--no-color"
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | local
| Bach      | yes

## Argument Reference
The following arguments are supported:
* `force` - (Optional) Adds `--force`. Default value is `false`.
* `frozen` - (Optional) Adds `--frozen-lockfile`. Default value is `true`.
* `args` - (Optional) Extra `pnpm install` arguments.
