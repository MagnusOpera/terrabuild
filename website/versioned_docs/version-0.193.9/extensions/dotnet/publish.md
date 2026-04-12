---
title: "publish"
---
Publishes binaries via `dotnet publish` for one project or a batch solution.
```
@dotnet publish {
    configuration = "Debug"
    restore = true
    build = true
    runtime = "linux-x64"
    trim = false
    single = false
    args = "--version-suffix beta"
    dependencies = true
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | remote
| Bach      | yes

## Argument Reference
The following arguments are supported:
* `configuration` - (Optional) Build configuration. Default value is `Debug`.
* `restore` - (Optional) Runs restore before publish. Default value is `true`.
* `build` - (Optional) Runs build before publish. Default value is `true`.
* `runtime` - (Optional) Runtime identifier (`--runtime`).
* `trim` - (Optional) Enables trimming. Default value is `false`.
* `single` - (Optional) Enables self-contained publish. Default value is `false`.
* `args` - (Optional) Extra `dotnet publish` arguments.
* `dependencies` - (Optional) When false, adds `--no-dependencies`. Default value is `true`.
