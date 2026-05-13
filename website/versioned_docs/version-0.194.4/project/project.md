---
title: Project Block

slug: /project/project
---

The `PROJECT` file defines one buildable unit inside a workspace.  
It is mandatory and must be placed at the root of each project directory.

## What Goes in `PROJECT`

Typical responsibilities:

- project identity and selection metadata (`identifier`, `labels`)
- change detection boundaries (`includes`, `ignores`)
- build output definition (`outputs`)
- project-level dependencies (`depends_on`)
- per-project extension configuration (`@dotnet`, `@pnpm`, `@docker`, ...)

In short: if a setting is specific to one project, place it in `PROJECT`.

## Minimal Example

```hcl
project {
  labels = [ "app" ]
  @dotnet { }
}

target build {
  @dotnet build { }
}
```

## Example Usage

```hcl
project api {
    outputs = [ "bin/" "obj/" "**/*.binlog" ]
    depends_on = [ project.api ]
    includes = [ "**/*" ]
    ignores = [ "**/*.binlog" ]
    environments = [ "staging", "dev*" ]
    labels = [ "app" "dotnet" ]

    @dotnet { }
}
```

## Argument Reference

The following arguments are supported:

* `identifier` - (Optional) Project identifier. Used to reference this project from other projects or targets. If not specified, the project path is used as the identifier.
* `outputs` - (Optional) List of glob patterns to include when capturing build artifacts. Patterns are relative to the project directory. These outputs are cached and can be restored in subsequent builds. When set in `PROJECT`, they are added to outputs inferred by extensions or defaults from workspace-level target configuration.
* `depends_on` - (Optional) List of project dependencies by identifier. The referenced projects must have a unique identifier. Note: this does not force loading the dependency, it just ensures the dependency is resolved before this project builds.
* `includes` - (Optional) List of glob patterns to determine which project files to include in change detection. Patterns are relative to the project directory. When set in `PROJECT`, they are added to Terrabuild's tracked include set, including script-derived files.
* `ignores` - (Optional) List of glob patterns to exclude from change detection. Patterns are relative to the project directory. Useful for ignoring generated files, logs, or temporary files. When set in `PROJECT`, this replaces the inferred/default ignore set instead of extending it.
* `environments` - (Optional) Specifies the list of environments in which this project is enabled. Values may include glob patterns (e.g. `dev*`, `*-blue`). Matching is case-insensitive. This attribute is only applied when the `--environment` flag is provided. If either `environments` is not set or no `--environment` is given, the project is considered enabled.
* `labels` - (Optional) List of labels used to select projects when using the `--label` flag. Default is a list of extension names used in the project (e.g., `["dotnet"]` if using `@dotnet`).
* `nested initializer blocks` - (Optional) Extension initializer blocks (e.g., `@dotnet { }`) that configure the project and can auto-discover dependencies, outputs, and other project properties.

:::info
Project-level `includes` and `outputs` are additive. `ignores` remains explicitly defined at the project level.
:::
