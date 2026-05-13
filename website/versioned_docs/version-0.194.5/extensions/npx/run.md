---
title: "run"
---
Executes a package binary via `npx --yes`.
```
@npx run {
    package = "hello-world-npm"
    args = ""
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
* `args` - (Optional) Arguments forwarded to the package.
