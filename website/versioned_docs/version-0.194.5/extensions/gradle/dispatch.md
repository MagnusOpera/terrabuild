---
title: "dispatch"
---
Runs an arbitrary Gradle command (action name is forwarded to `gradle`).
```
@gradle dispatch {
    args = "clean --info"
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | never
| Bach      | no

## Argument Reference
The following arguments are supported:
* `args` - (Optional) Extra arguments for the Gradle command.
