---
title: "restore"
---
Restores NuGet packages for one project or a batch solution.
```
@dotnet restore {
    locked = true
    evaluate = false
    args = "--force"
    dependencies = true
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | local
| Bach      | yes

## Argument Reference
The following arguments are supported:
* `locked` - (Optional) Adds `--locked-mode`. Default value is `true`.
* `evaluate` - (Optional) Adds `--force-evaluate`. Default value is `false`.
* `args` - (Optional) Extra `dotnet restore` arguments.
* `dependencies` - (Optional) When false, adds `--no-dependencies`. Default value is `true`.
