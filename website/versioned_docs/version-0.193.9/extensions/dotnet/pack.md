---
title: "pack"
---
Packs the project into a NuGet package via `dotnet pack`.
```
@dotnet pack {
    configuration = "Debug"
    restore = true
    build = true
    version = "1.0.0"
    args = "--include-symbols"
    dependencies = true
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | remote
| Bach      | no

## Argument Reference
The following arguments are supported:
* `configuration` - (Optional) Build configuration. Default value is `Debug`.
* `restore` - (Optional) Runs restore before pack. Default value is `true`.
* `build` - (Optional) Runs build before pack. Default value is `true`.
* `version` - (Optional) Package version (`/p:Version`).
* `args` - (Optional) Extra `dotnet pack` arguments.
* `dependencies` - (Optional) When false, adds `--no-dependencies`. Default value is `true`.
