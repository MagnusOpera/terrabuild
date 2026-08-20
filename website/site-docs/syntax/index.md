---
title: Syntax

---

Terrabuild reads two configuration files:

- `WORKSPACE` sits at the repository root and defines shared configuration.
- `PROJECT` sits at the root of a project and defines project-specific configuration.

Both use the same HCL-inspired syntax, but they accept different blocks and attributes.

- A comment starts with `#` and ends at the newline.
- A block defines a structure such as `project`, `target`, or `extension`.
- An attribute assigns an expression to a name.
- Declaration order does not matter. Command order inside a target does.

## Workspace file

`WORKSPACE` is required at the repository root. It defines target dependencies, optional phases, variables, and extension defaults.

``` {filename="WORKSPACE"}
# Build upstream project dependencies first.
target build {
  depends_on = [ target.^build ]
}

# This target has no shared dependencies.
target docs { }

# Publishing requires both build and docs.
target publish {
  build = ~always
  depends_on = [ target.build, target.docs ]
}

# Default build configuration.
variable config {
  description = "configuration to build"
  default = "Debug"
}

# Run .NET actions in a pinned SDK image.
extension @dotnet {
  image = "mcr.microsoft.com/dotnet/sdk:8.0"
  defaults {
    configuration = var.config
  }
}

# Run npm actions in a pinned Node image.
extension @npm {
  image = "node:20"
}

# Run Docker actions on the host and share these defaults.
extension @docker {
  defaults {
    arguments = { configuration: var.config }
    image = "ghcr.io/example/${terrabuild.project}"
  }
}
```

## Project file

Each project requires a `PROJECT` file. It defines project metadata, target commands, outputs, and optional phase assignments.

In `PROJECT`, `includes` and `outputs` are merged with inferred/default values for that project. `ignores` remains an explicit project-level set.

``` {filename="PROJECT"}

# Project files and outputs.
project {
    # Ignore files that do not affect target output.
    ignores = [ "**/*.binlog" ]
  
    # Capture these paths after a cacheable target runs.
    outputs = [ "bin/", "obj/", "**/*.binlog" ]
}

# Commands run in declaration order.
target build {
    depends_on = [ target.prepare ]
    # Pass the log argument to the .NET action.
    @dotnet publish { log = true }

    # This action uses its configured defaults.
    @docker build { }
}

# Push the image built above.
target publish {
    @docker push { }
}
```

Some extensions discover project dependencies, tracked files, and outputs through an initializer block.

``` {filename="PROJECT"}
# Let the .NET extension discover project metadata.
project {
    # Labels can filter a run.
    labels = [ "app", "dotnet" ]
    @dotnet { }
}

target build {
    @dotnet publish { log = true }
    @docker build { }
}

target publish {
    @docker push { }
}
```

## Scope across workspace and project

This example shows which values originate in `WORKSPACE` and which values a `PROJECT` file adds.

``` {filename="WORKSPACE"}
# Workspace-level variable - can be overridden via command line or environment
variable config {
  description = "Build configuration"
  default = "Debug"
}

variable environment {
  description = "Target environment"
  default = "dev"
}

# Workspace-level local - computed from variables
locals {
  image_tag = var.environment + "-" + terrabuild.branch_or_tag
  registry = "ghcr.io/myorg"
}

# Global extension configuration - applies to all projects
extension @dotnet {
  image = "mcr.microsoft.com/dotnet/sdk:8.0"
  defaults {
    configuration = var.config
  }
}

extension @docker {
  defaults {
    registry = local.registry
    tag = local.image_tag
  }
}

# Workspace-level target dependency rules
target build {
  depends_on = [ target.^build ]
}
```

``` {filename="src/apps/api/PROJECT"}
# Project-level local - can use workspace variables and locals
locals {
  app_name = "api"
  full_image = local.registry + "/" + local.app_name + ":" + local.image_tag
}

# Project-level extension specialization adds defaults to inherited entries.
extension @docker {
  defaults {
    image = local.full_image
    platform = "linux/amd64"
  }
}

# Project configuration
project {
  labels = [ "app", "api" ]
  @dotnet { }
}

# Project target - uses workspace variable and project local
target build {
  @dotnet build { 
    configuration = var.config
  }
}

target dist {
  @dotnet publish { 
    runtime = "linux-x64"
    configuration = var.config
  }
  @docker build { 
    image = local.full_image
  }
}
```

The scoping rules are:

- `var.*` values are declared in `WORKSPACE` and can be overridden by the command line or environment.
- A workspace `local.*` value can use variables and predefined values.
- A project `local.*` value can also use workspace locals.
- A project extension can specialize inherited scalar values. Its `variables` are additive. Its `defaults` and `env` maps can add keys but cannot replace or remove inherited entries.
- Expressions can combine values and functions, as in `local.registry + "/" + local.app_name`.

See [Variables](/docs/workspace/variable), [Locals](/docs/workspace/locals), and [Extensions](/docs/workspace/extension) for detailed reference.

- [Scaffolding](/docs/getting-started/scaffolding)
- [Workspace reference](/docs/workspace)
- [Project reference](/docs/project)
- [Extension authoring](/docs/extensibility)
