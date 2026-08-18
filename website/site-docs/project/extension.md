---
title: Extension Block

---

The `extension` block in `PROJECT` specializes workspace-level extension configuration for the current project.

## Specialization semantics

Terrabuild specializes an extension field by field:

- A field omitted from `PROJECT` inherits its value from `WORKSPACE`.
- Scalar fields declared in `PROJECT` replace their workspace value. Use `nothing` to clear an inherited optional scalar where the field accepts it.
- `variables` is additive. Project entries are combined with inherited workspace entries and duplicates are ignored.
- `defaults` and `env` may only add new keys. Replacing or removing an inherited key is rejected as a configuration error.
- Empty project collections have no effect on inherited entries.

This monotonic collection model keeps independently configured projects safe to combine in an optimized batch.

For example, this project inherits the workspace `image`, `variables`, `defaults`, and `env`, then adds two environment entries and replaces `platform`:

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

A target that uses this `@docker` extension must declare
`environment_sensitive = true` because the extension consumes
`terrabuild.head_commit`.

## Arguments

- `identifier` (required): extension identifier.
- `image` (optional): container image used to run extension actions.
- `platform` (optional): target container platform.
- `cpus` (optional): max CPUs for container execution.
- `variables` (optional): additional host env variable names forwarded to the container.
- `defaults` (optional): additional default action arguments; inherited keys cannot be replaced.
- `env` (optional): additional environment values; inherited keys cannot be replaced.
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
