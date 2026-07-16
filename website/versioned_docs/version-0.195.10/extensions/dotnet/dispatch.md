---
title: "dispatch"
---
Runs an arbitrary `dotnet` command (action name is forwarded to `dotnet`).
```
@dotnet dispatch {
    args = "run -- -v"
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | never
| Bach      | no

## Argument Reference
The following arguments are supported:
* `args` - (Optional) Extra arguments for `dotnet`.
