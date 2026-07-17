---
title: Extensibility

---

Terrabuild extensions are FScript programs that translate target actions into shell operations. They let a workspace add a tool-specific vocabulary without compiling or modifying Terrabuild.

:::tip Learn FScript first
FScript is a lightweight language with F#/ML-style functions, records, unions, options, collections, and pattern matching.

- [Start the FScript tutorial](https://magnusopera.github.io/FScript/manual/0.75.0/learn/quickstart)
- [Read the language manual](https://magnusopera.github.io/FScript/manual/0.75.0/)
- [Try FScript in the browser](https://magnusopera.github.io/FScript/sandbox)

Terrabuild currently embeds FScript `0.75.0`; the links above open the matching version of the manual.
:::

Use extensions to:
- expose project defaults (`default` handler)
- expose runnable actions (`dispatch` or explicit command handlers)
- execute tools consistently on host or in containers

Terrabuild ships built-in extensions (identifiers like `@dotnet`, `@npm`, `@terraform`) and also supports custom extensions implemented as FScript (`.fss`).

:::info
`@...` identifiers are reserved for built-in extensions.
Use non-`@` identifiers for custom extensions.
:::

## Two Layers to Learn

The FScript documentation explains the language: values, functions, type annotations, modules, imports, exports, and the standard library.

The Terrabuild documentation explains the host protocol layered on top:

- extension entrypoints use `[<export>] let`
- the first parameter is always named `context`
- remaining parameters bind exact, case-sensitive target argument names
- command handlers return `{ Batchable; Operations }`
- the script's final value describes dispatch, defaults, and cacheability
- Terrabuild supplies a restricted set of host functions and confines filesystem access to the workspace

Start with [FScript Extensions](./script), then use [Protocol Types](./types) and [Host Functions](./functions) as references.

## Authoring Path

1. Learn enough [FScript](https://magnusopera.github.io/FScript/manual/0.75.0/learn/quickstart) to read functions, records, options, and pattern matching.
2. Write a local `.fss` script with one exported command handler.
3. Add the descriptor that declares dispatch behavior and cacheability.
4. Register the script in an `extension` block in `WORKSPACE` or `PROJECT`.
5. Invoke its actions from targets and add defaults, metadata discovery, or batching only when needed.

Container execution is configured independently from the script protocol; see [Container](./container).
