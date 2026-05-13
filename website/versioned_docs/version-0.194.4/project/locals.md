---
title: Locals Block

---

The `locals` block defines computed values at project scope.

Project locals can reference workspace variables, workspace locals, predefined values, and other project locals.
They are only visible inside the current `PROJECT` file.

## Usage

Project locals are referenced with `local.<identifier>`.

## Example Usage

### Project-Only Values

```
locals {
  app_name = "api"
  project_path = "src/apps/api"
}
```

### Using Workspace Values

```
# In WORKSPACE:
locals {
  registry = "ghcr.io/myorg"
}

# In PROJECT:
locals {
  app_name = "api"
  # Reference workspace local
  full_image = local.registry + "/" + local.app_name
}
```

### Derived Project Configuration

```
# In WORKSPACE:
variable environment {
  default = "dev"
}
locals {
  registry = "ghcr.io/myorg"
  base_tag = var.environment + "-" + terrabuild.branch_or_tag
}

# In PROJECT:
locals {
  app_name = "api"
  image_name = local.registry + "/" + local.app_name
  image_tag = local.base_tag
  full_image = local.image_name + ":" + local.image_tag
  
  docker_args = {
    image: local.full_image
    platform: "linux/amd64"
  }
}

extension @docker {
  defaults = local.docker_args
}
```

## Rules

- Project locals cannot redefine a workspace local with the same name.
- Project locals are not visible to other projects.
- Multiple `locals` blocks are allowed as long as identifiers stay unique.
