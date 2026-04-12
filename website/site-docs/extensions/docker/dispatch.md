---
title: "dispatch"
---
Runs an arbitrary Docker CLI command (action name is forwarded to `docker`).
```
@docker dispatch {
    args = "image prune -f"
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | never
| Bach      | no

## Argument Reference
The following arguments are supported:
* `args` - (Optional) Extra arguments for the Docker subcommand.
