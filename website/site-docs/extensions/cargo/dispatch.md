---
title: "dispatch"
---
Runs an arbitrary Cargo subcommand (Terrabuild action name is forwarded to `cargo`).
```
@cargo dispatch {
    args = "check --locked"
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | never
| Bach      | no

## Argument Reference
The following arguments are supported:
* `args` - (Optional) Extra arguments for the subcommand.
