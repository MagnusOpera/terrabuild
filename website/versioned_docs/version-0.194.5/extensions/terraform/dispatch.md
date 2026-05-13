---
title: "dispatch"
---
Runs an arbitrary Terraform command (action name is forwarded to `terraform`).
```
@terraform dispatch {
    args = "fmt -write=false"
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | never
| Bach      | no

## Argument Reference
The following arguments are supported:
* `args` - (Optional) Extra arguments for the Terraform subcommand.
