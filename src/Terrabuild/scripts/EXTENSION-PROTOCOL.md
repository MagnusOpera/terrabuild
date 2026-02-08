# Terrabuild FScript Extension Protocol

This document defines the normative protocol between Terrabuild and FScript extensions.

## Conformance Language

The keywords **MUST**, **MUST NOT**, **SHOULD**, and **MAY** are normative.

## 1. Exported Functions

1. Extension entrypoints **MUST** be declared with `export let`.
2. Terrabuild **MUST** invoke only exported functions.
3. Descriptor entries **MUST** reference exported function names only.

## 2. Function Signature Contract

For every exported extension function:

1. The first parameter **MUST** be named `context`.
2. The first parameter **MUST** represent the action context shape.
3. Terrabuild **MUST** pass `context` as the first invocation argument.
4. Remaining parameters **MUST** be bound by exact parameter name (case-sensitive) from target arguments.
5. Parameter name matching **MUST NOT** perform case conversion, aliasing, or fallback names.

Example:

```fsharp
export let dispatch (context: { Command: string }) (variables: string map option) (args: string option) =
  ...
```

## 3. Argument Binding Rules

1. Terrabuild **MUST** provide `context` for exported function invocations.
2. If a non-context parameter is missing:
   - Terrabuild **MUST** pass `None` when the parameter type is `option<_>`.
   - Terrabuild **MUST** fail invocation for any non-optional parameter.
3. Extra target keys not declared in the function signature **MAY** be ignored.
   - Current Terrabuild behavior: ignored.

## 4. Context Shape

1. Context is structurally typed.
2. Scripts **MAY** use partial record annotations for context.
3. Terrabuild currently provides fields compatible with `ActionContext` (for example `Command`, `Debug`, `CI`, `Hash`, `Batch`).

Canonical host shape:

```fsharp
type BatchContext = {
  Hash: string
  TempDir: string
  ProjectPaths: string list
}

type ActionContext = {
  Debug: bool
  CI: bool
  Command: string
  Hash: string
  Batch: BatchContext option
}
```

Partial annotation is valid and preferred when only a subset is needed:

```fsharp
export let dispatch (context: { Command: string }) ...
```

## 5. Descriptor Contract

The script descriptor **MUST** be a map from function name to flag list.

Canonical form:

```fsharp
#{
  nameof dispatch = ["dispatch"; "never"]
}
```

Supported flags:

- `dispatch`
- `default`
- `batchable`
- cacheability: `never`, `local`, `external`, `remote`

## 6. Method Resolution

1. Command resolution **MUST** use exact exported function name when present.
2. If no exact command function exists, Terrabuild **MUST** use the function flagged `dispatch` when present.
3. Default metadata resolution **MUST** use the function flagged `default` when present.

## 7. Return Contract

1. Command handlers **MUST** return `ShellOperations` compatible values.
2. Default handlers **MUST** return `ProjectInfo` compatible values.

Expected shapes:

```fsharp
type ShellOperation = {
  Command: string
  Arguments: string
  ErrorLevel: int
}

type ShellOperations = ShellOperation list

type ProjectInfo = {
  Id: string option
  Outputs: Set<string>
  Dependencies: Set<string>
}
```

Command handler example:

```fsharp
[{ Command = "make"; Arguments = "build"; ErrorLevel = 0 }]
```

Default handler example:

```fsharp
{ Id = None; Outputs = []; Dependencies = [] }
```

## 8. Built-In Script Contract Examples

- `shell.fss`: `dispatch (context) (args)`
- `make.fss`: `dispatch (context) (variables) (args)`
- `npx.fss`: `run (context) (package) (args)`
- `null.fss`: exported handlers follow the same `context`-first rule

## 9. Protocol Evolution

Any protocol change **MUST** be introduced in this document before implementation.
