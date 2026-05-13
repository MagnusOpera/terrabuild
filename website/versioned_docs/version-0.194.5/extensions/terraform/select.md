---
title: "select"
---
Selects or creates a Terraform workspace before planning/applying.
```
@terraform select {
    workspace = "dev"
    create = true
    args = "-no-color"
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | never
| Bach      | no

## Argument Reference
The following arguments are supported:
* `workspace` - (Optional) Workspace name (`default` when omitted).
* `create` - (Optional) Adds `-or-create`. Default value is `true`.
* `args` - (Optional) Extra `terraform workspace select` arguments.
