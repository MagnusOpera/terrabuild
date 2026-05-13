---
title: "build"
---
Invokes `gradle assemble` for the chosen configuration.
```
@gradle build {
    configuration = "Debug"
    args = "--scan"
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
* `args` - (Optional) Extra `gradle assemble` arguments.
