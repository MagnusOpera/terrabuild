---
title: "destroy"
---
Destroys all managed resources for the current workspace.
```
@terraform destroy {
    variables = { configuration: "Release" }
    args = ""
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | remote
| Bach      | no

## Argument Reference
The following arguments are supported:
* `variables` - (Optional) Variables passed as `-var` assignments.
* `args` - (Optional) Extra `terraform destroy` arguments.
