---
title: "dispatch"
---
Runs an arbitrary pnpm command (Terrabuild action name is forwarded to `pnpm`).
```
@pnpm dispatch {
    args = "store status"
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | never
| Bach      | no

## Argument Reference
The following arguments are supported:
* `args` - (Optional) Extra arguments for the pnpm command.
