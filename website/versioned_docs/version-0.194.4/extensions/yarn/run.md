---
title: "run"
---
Runs an arbitrary yarn script (`yarn <command>`).
```
@yarn run {
    command = "build:prod"
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
* `command` - (Required) Script name.
* `args` - (Optional) Extra arguments after `--`.
