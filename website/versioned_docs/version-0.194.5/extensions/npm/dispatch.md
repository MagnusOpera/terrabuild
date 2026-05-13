---
title: "dispatch"
---
Runs an arbitrary npm command (forwards the Terrabuild action name to `npm`).
```
@npm dispatch {
    args = "cache verify"
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | never
| Bach      | no

## Argument Reference
The following arguments are supported:
* `args` - (Optional) Extra arguments for the npm command.
