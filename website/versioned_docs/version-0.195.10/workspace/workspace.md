---
title: Workspace Block

slug: /workspace/workspace
---

The `WORKSPACE` file configures the repository at the root level.
Use it for settings that should apply across many projects.

## What Goes in `WORKSPACE`

Typical responsibilities:

- workspace-wide defaults (`configuration`, `environment`, `engine`)
- project discovery/exclusion (`ignores`)
- script filesystem sandbox restrictions (`deny`)
- global targets (`build`, `test`, `dist`) and dependency policies
- shared extension defaults (for example `@dotnet`, `@pnpm`)
- optional shared-cache identity (`id`)

In short: if a setting should be shared, put it in `WORKSPACE`.

## Minimal Example

```hcl
workspace {
  engine = ~docker
  configuration = "local"
  environment = "dev"
}

target build {
  depends_on = [ target.^build ]
}

target test {
  depends_on = [ target.build ]
}
```

## Example Usage

```hcl
workspace {
    id = "af628998-bd53-4063-b054-f0b87965edd4"
    ignores = [ "src/project-1" ]
    deny = [ ".git", "**/node_modules", "**/bin", "**/obj" ]
    version = "0.174.0"
    engine = ~docker
    configuration = "local"
    environment = "dev"
}
```

## Argument Reference

The following arguments are supported:

* `id` - (Optional) Workspace Id.
* `ignores` - (Optional) List of projects to ignore (globbings). Globbings are relative to workspace folder. 

  Default value includes common build and dependency directories:
  ```
  [
      "node_modules"
      ".pnpm-store"
      ".terrabuild"
      "bin"
      "obj"
      "dist"
  ]
  ```
  
  These defaults ensure that common build artifacts and dependency directories are automatically excluded from project discovery.
* `deny` - (Optional) List of glob paths denied to FScript filesystem external functions.

  Example:
  ```
  [
      ".git"
      "**/node_modules"
      "**/bin"
      "**/obj"
  ]
  ```

  Default value:
  ```
  [
      ".git"
  ]
  ```

  Paths are evaluated from workspace root and used by extension script sandboxing.
* `version` - (Optional) Minimal Terrabuild version required by this workspace. Default is `nothing`.
* `engine` - (Optional) Container engine to use. Allowed values are `~docker`, `~podman`, and `~none`. Default is `~docker`.
* `configuration` - (Optional) Default configuration value exposed to the workspace. Default is `nothing`.
* `environment` - (Optional) Default environment value exposed to the workspace. Default is `nothing`.

## Shared Cache

If you use a managed shared cache, set `workspace.id` to the workspace identifier and authenticate through the CLI:

```bash
terrabuild login --workspace <workspaceId> --token <token>
```

Use `--local-only` when you want to ignore the shared cache for a run.
