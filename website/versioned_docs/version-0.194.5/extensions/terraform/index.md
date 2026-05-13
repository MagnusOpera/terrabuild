---
title: "terraform"
---



## Available Commands
| Command | Description |
|---------|-------------|
| [dispatch](/docs/extensions/terraform/dispatch) | Runs an arbitrary Terraform command (action name is forwarded to `terraform`). |
| [init](/docs/extensions/terraform/init) | Initializes Terraform providers and backend. |
| [validate](/docs/extensions/terraform/validate) | Generate plan file. |
| [select](/docs/extensions/terraform/select) | Select workspace. |
| [plan](/docs/extensions/terraform/plan) | Generate plan file. |
| [apply](/docs/extensions/terraform/apply) | Apply plan file. |
| [destroy](/docs/extensions/terraform/destroy) | Destroy the deployment. |

## Project Initializer
Declares default outputs for Terraform actions.
```
project {
  @terraform { }
}
```
Equivalent to:
```
project {
    ignores = [ ".terraform/" "*.tfstate/" ]
    outputs = [ "*.planfile" ]
}
```
