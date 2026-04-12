---
title: "push"
---
Pushes the built image to the registry with a specific tag.
```
@docker push {
    image = "ghcr.io/example/project"
    tag = "1.2.3-stable"
    args = "--disable-content-trust"
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | external
| Bach      | no

## Argument Reference
The following arguments are supported:
* `image` - (Required) Image repository.
* `tag` - (Required) Target tag.
* `args` - (Optional) Extra tagging/push arguments.
