---
title: "run"
---
Runs an arbitrary pnpm script (`pnpm run <target>`).
```
@pnpm run {
    target = "build:prod"
    args = "--watch"
    recursive = true
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | local
| Bach      | no

## Argument Reference
The following arguments are supported:
* `target` - (Required) Script name.
* `args` - (Optional) Extra script arguments.
* `recursive` - (Optional) Adds `--recursive`. Default value is `true`.
