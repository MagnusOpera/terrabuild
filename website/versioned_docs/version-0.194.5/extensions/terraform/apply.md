---
title: "apply"
---
Applies the generated plan (or runs `apply` without a plan when requested).
```
@terraform apply {
    args = "-no-color"
    plan = false
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | remote
| Bach      | no

## Argument Reference
The following arguments are supported:
* `args` - (Optional) Extra `terraform apply` arguments.
* `plan` - (Optional) Skips the generated plan file when true. Default value is `false`.
