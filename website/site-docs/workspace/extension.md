---
title: Extension Block

---

The `extension` block defines extension configuration at workspace scope.
These settings apply globally and can be overridden in `PROJECT` files.

## Example

```hcl
extension @dotnet {
  image = "mcr.microsoft.com/dotnet/sdk:8.0"
  platform = "linux/arm64"
  cpus = 2
  variables = [ "NUGET_KEY" ]
  defaults = {
    configuration = var.configuration
  }
}

extension npm_ci {
  script = "tools/extensions/npm-ci.fss"
}
```

## Arguments

- `identifier` (required): extension identifier.
- `image` (optional): container image used to run extension actions.
- `platform` (optional): target container platform (`linux/amd64`, `linux/arm64`, ...).
- `cpus` (optional): max CPUs for container execution (strictly positive).
- `variables` (optional): host env variable names forwarded to container.
- `defaults` (optional): default action arguments for this extension.
- `script` (optional): scripted implementation source.

## Identifier conventions

- Built-in extensions use `@...` identifiers (for example `@dotnet`, `@npm`, `@terraform`).
- Custom extensions should use non-`@` identifiers.

## Script sources

`script` supports:
- local `.fss` path inside the workspace
- HTTPS URL to a `.fss` script

See [Script Extensibility](/docs/extensibility/script).
