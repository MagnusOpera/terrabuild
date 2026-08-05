---
title: Extension Block

---

The `extension` block in `PROJECT` specializes workspace-level extension configuration for the current project.

## Override semantics

Terrabuild overlays an extension field by field, using the same shallow override model as project targets:

- A field omitted from `PROJECT` inherits its value from `WORKSPACE`.
- A field declared in `PROJECT` replaces the complete workspace value for that field.
- Collection fields are atomic. `variables`, `defaults`, and `env` are replaced as whole collections and are never merged by item or key.
- Use an empty collection to clear an inherited collection: `variables = []`, `defaults {}`, or `env {}`.
- Use `nothing` to clear an inherited optional value where the field accepts it.

For example, this project inherits the workspace `image`, `variables`, and `defaults`, but replaces the complete `platform` and `env` fields:

## Example

```hcl
extension @docker {
  platform = "linux/amd64"
  env {
    IMAGE = "ghcr.io/example/app"
    TAG = terrabuild.head_commit
  }
}

extension npm_ci {
  script = "tools/extensions/npm-ci.fss"
}
```

## Arguments

- `identifier` (required): extension identifier.
- `image` (optional): container image used to run extension actions.
- `platform` (optional): target container platform.
- `cpus` (optional): max CPUs for container execution.
- `variables` (optional): complete replacement for the host env variable names forwarded to the container in this project.
- `defaults` (optional): complete replacement for the extension's default action arguments in this project.
- `env` (optional): complete replacement for the environment values added to every action in this project.
- `script` (optional): scripted implementation source.

## Identifier conventions

- Built-in extensions use `@...` identifiers.
- Custom extensions should use non-`@` identifiers.

## Script sources

`script` supports:
- local `.fss` path inside the workspace
- HTTPS URL to a `.fss` script

A custom extension specialization may omit `script` to inherit its workspace implementation.

See [Script Extensibility](/docs/extensibility/script).
