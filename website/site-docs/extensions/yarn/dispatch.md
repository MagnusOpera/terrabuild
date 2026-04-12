---
title: "dispatch"
---
Runs an arbitrary yarn command (Terrabuild action name is forwarded to `yarn`).
```
@yarn dispatch {
    args = "cache clean"
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | never
| Bach      | no

## Argument Reference
The following arguments are supported:
* `args` - (Optional) Extra arguments for the yarn command.
