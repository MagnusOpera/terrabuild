---
title: Extensibility

---

Terrabuild extensions are script-driven.

Use extensions to:
- expose project defaults (`default` handler)
- expose runnable actions (`dispatch` or explicit command handlers)
- execute tools consistently on host or in containers

Terrabuild ships built-in extensions (identifiers like `@dotnet`, `@npm`, `@terraform`) and also supports custom extensions implemented as FScript (`.fss`).

:::info
`@...` identifiers are reserved for built-in extensions.
Use non-`@` identifiers for custom extensions.
:::

## Authoring Model

Custom extensions follow the Terrabuild FScript protocol:
- exported handlers are declared with `export let`
- first parameter is always `context`
- command arguments are bound by exact, case-sensitive parameter names
- a descriptor map declares capabilities with DU flags (`Dispatch`, `Default`, `Batchable`, `Never|Local|External|Remote`)

See:
- [Script](/docs/extensibility/script)
- [Types](/docs/extensibility/types)
- [Container](/docs/extensibility/container)

## FScript

FScript is the scripting language/runtime used for custom extensions:
- https://github.com/MagnusOpera/FScript
