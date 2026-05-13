---
title: "build"
---
Builds the project or batch solution via `dotnet build`.
```
@dotnet build {
    configuration = "Debug"
    parallel = 1
    log = false
    restore = true
    version = "1.2.3"
    args = "--no-incremental"
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
* `parallel` - (Optional) Max CPU count (`-maxcpucount`).
* `log` - (Optional) Adds `-bl`. Default value is `false`.
* `restore` - (Optional) Runs restore before build. Default value is `true`.
* `version` - (Optional) Sets MSBuild `Version`.
* `args` - (Optional) Extra `dotnet build` arguments.
* `dependencies` - (Optional) When false, adds `--no-dependencies`. Default value is `true`.
