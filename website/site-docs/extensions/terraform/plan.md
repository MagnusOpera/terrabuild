---
title: "plan"
---
Generates a plan file for review/apply.
```
@terraform plan {
    variables = { configuration: "Release" }
    args = "-no-color"
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
* `args` - (Optional) Extra `terraform plan` arguments.
