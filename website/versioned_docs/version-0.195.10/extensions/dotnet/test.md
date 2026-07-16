---
title: "test"
---
Runs tests via `dotnet test`, optionally batched through a generated solution.
```
@dotnet test {
    configuration = "Debug"
    restore = true
    build = true
    filter = "TestCategory!=integration"
    args = "--blame-hang"
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
* `restore` - (Optional) Runs restore before tests. Default value is `true`.
* `build` - (Optional) Runs build before tests. Default value is `true`.
* `filter` - (Optional) Filter expression (`--filter`).
* `args` - (Optional) Extra `dotnet test` arguments.
