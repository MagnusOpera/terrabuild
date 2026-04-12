---
title: "exec"
---
Executes a package binary via `npm exec`.
```
@npm exec {
    package = "openapi-generator-cli"
    args = "version-manager list"
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | local
| Bach      | no

## Argument Reference
The following arguments are supported:
* `package` - (Required) Package to execute.
* `args` - (Optional) Arguments for the package command.
