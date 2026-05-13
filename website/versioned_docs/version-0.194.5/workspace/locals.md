---
title: Locals Block

---

The `locals` block defines computed values at workspace scope.
Unlike `variable`, a local is not overridden from the CLI or environment.

Workspace locals are available in both `WORKSPACE` and `PROJECT`.

## Usage

Locals are referenced with `local.<identifier>`.
They can reference variables, predefined values, functions, and other locals.

## Example Usage

### Simple Values

```
locals {
  app_name = "terrabuild"
  version = "1.0.0"
}
```

### Computed Values

```
variable environment {
  default = "dev"
}

locals {
  image_tag = var.environment + "-" + terrabuild.branch_or_tag
  registry = "ghcr.io/myorg"
}
```

### Using in Extensions

```
locals {
  dotnet_version = "8.0"
  node_version = "22"
}

extension @dotnet {
  image = "mcr.microsoft.com/dotnet/sdk:" + local.dotnet_version
}

extension @npm {
  image = "node:" + local.node_version
}
```

## Rules

- Multiple `locals` blocks are allowed in the same file.
- Identifiers must stay unique across all `locals` blocks.
- Workspace locals are visible from project files.

```hcl
locals {
  app_name = "api"
}

locals {
  version = "1.0.0"
}
```
