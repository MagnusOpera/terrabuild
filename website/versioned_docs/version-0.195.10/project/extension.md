---
title: Extension Block

---

The `extension` block in `PROJECT` overrides or augments workspace-level extension configuration for the current project.

## Example

```hcl
extension @docker {
  platform = "linux/amd64"
  defaults = {
    image = local.image
    tag = terrabuild.head_commit
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
- `variables` (optional): host env variable names forwarded to container.
- `defaults` (optional): default action arguments for this extension in this project.
- `script` (optional): scripted implementation source.

## Identifier conventions

- Built-in extensions use `@...` identifiers.
- Custom extensions should use non-`@` identifiers.

## Script sources

`script` supports:
- local `.fss` path inside the workspace
- HTTPS URL to a `.fss` script

See [Script Extensibility](/docs/extensibility/script).
