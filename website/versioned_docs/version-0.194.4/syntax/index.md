---
title: Syntax

---

There are two different configuration file types in Terrabuild:
* **WORKSPACE** - Located at the root of the workspace, defines global configuration
* **PROJECT** - Located at the root of each project, defines project-specific configuration

Both share the same syntax - but not the same functionalities - and are based on a simplified HCL (HashiCorp Configuration Language) syntax:
* Comments are single-line and start with a `#`
* Blocks - define structures like `project`, `target`, `extension`, etc.
* Attributes - key-value pairs within blocks
* Order is not important except for commands within a target

## WORKSPACE
WORKSPACE is a mandatory file at the root of your repository. It describes targets dependencies, configurations and default extensions configuration.

``` {filename="WORKSPACE"}
# before building, we want all dependencies to be completed
target build {
  depends_on = [ target.^build ]
}

# docs has no configuration (hence no dependencies)
target docs { }

# to publish, we need both build and docs to be completed
# also this target is always built regardless of caching status
target publish {
  depends_on = [ target.build, target.docs ]
}

# configuration used by default
variable config {
  description = "configuration to build"
  default = "Debug"
}

# configure the internal extension @dotnet
# it uses a build image and provides default parameters for all actions of @dotnet
extension @dotnet {
  image = "mcr.microsoft.com/dotnet/sdk:8.0"
  defaults {
    configuration = var.config
  }
}

# @npm extension uses only a build image
extension @npm {
  image = "node:20"
}

# @docker extension is built without a container image override (hence must be deployed on host)
# also default parameters are provided for all actions of @docker 
extension @docker {
  defaults {
    arguments = { configuration: var.config }
    image = "ghcr.io/example/${terrabuild.project}"
  }
}
```

## PROJECT
PROJECT is a mandatory file for each project. It defines how the project shall be built. It also describes outputs.

In `PROJECT`, `includes` and `outputs` are merged with inferred/default values for that project. `ignores` remains an explicit project-level set.

``` {filename="PROJECT"}

# provide the configuration for project - note
project {
    # a list of files/directory to ignore (globbing format)
    ignores = [ "**/*.binlog" ]
  
    # outputs to cache
    outputs = [ "bin/", "obj/", "**/*.binlog" ]
}

# this is the implementation of the build target
# it provides a list of action to run in order to complete the target
target build {
    depends_on = [ target.prepare ]
    # invoke the publish command - also pass log parameter
    @dotnet publish { log = true }

    # invoke docker build command - parameter are optional
    @docker build { }
}

# another target implementation
target publish {
    @docker push { }
}
```

Some extensions provide an `init` capability to discover and configure automatically the project defaults.

``` {filename="PROJECT"}
# @dotnet extension is able to find project to build, ignores, outputs, and all dependencies
project {
    # define labels for scoping the build
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

## Understanding Variable, Local, and Extension Scope

The interplay between workspace and project blocks, along with variables, locals, and extensions, can be complex. Here's a comprehensive example showing how they work together:

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

# Project-level extension override - overrides workspace defaults
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

**Key Points:**
- **Variables** (`var.*`) are declared in WORKSPACE and can be overridden
- **Workspace locals** (`local.*` in WORKSPACE) are computed from variables and predefined values
- **Project locals** (`local.*` in PROJECT) can reference workspace variables and locals
- **Extensions** can be configured globally (WORKSPACE) and overridden per-project (PROJECT)
- **Project extensions** can use both workspace and project locals
- **Expressions** can combine variables, locals, and functions: `local.registry + "/" + local.app_name`

See [Variables](/docs/workspace/variable), [Locals](/docs/workspace/locals), and [Extensions](/docs/workspace/extension) for detailed reference.

- [scaffolding](/docs/getting-started/scaffolding)
- [workspace](/docs/workspace)
- [project](/docs/project)
- [extensibility](/docs/extensibility)
