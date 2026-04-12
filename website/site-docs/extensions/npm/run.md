---
title: "run"
---
Runs an arbitrary npm script via `npm run <target>`.
```
@npm run {
    target = "build:prod"
    args = "--watch"
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
* `args` - (Optional) Extra arguments after `--`.
