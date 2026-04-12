---
title: Extensions

---

Terrabuild ships built-in extensions for common toolchains such as .NET, npm, pnpm, Docker, Terraform, and more.

Built-in extension identifiers start with `@`.

```hcl
project {
  @npm { }
}

target build {
  @npm build { }
  @docker build { }
}
```

Use extension configuration in:

- `WORKSPACE` for shared defaults
- `PROJECT` for project-specific overrides

Each command page below documents one action and its arguments.
For custom extensions, see [Extensibility](/docs/extensibility).
