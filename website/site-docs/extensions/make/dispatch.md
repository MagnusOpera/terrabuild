---
title: "dispatch"
---
Invokes the Terrabuild action name as the make target.
```
@make dispatch {
    variables = { configuration: "Release" }
    args = "-d"
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | never
| Bach      | no

## Argument Reference
The following arguments are supported:
* `variables` - (Optional) `KEY=VALUE` pairs before the target.
* `args` - (Optional) Extra `make` arguments.
