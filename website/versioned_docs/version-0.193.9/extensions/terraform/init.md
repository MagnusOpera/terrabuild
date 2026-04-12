---
title: "init"
---
Initializes Terraform providers and backend.
```
@terraform init {
    config = "backend.prod.config"
    args = "-upgrade"
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | local
| Bach      | no

## Argument Reference
The following arguments are supported:
* `config` - (Optional) Backend config file.
* `args` - (Optional) Extra `terraform init` arguments.
