# Terrabuild FScript Extension Protocol

This document defines the normative protocol between Terrabuild and FScript extensions.

## Conformance Language

The keywords **MUST**, **MUST NOT**, **SHOULD**, and **MAY** are normative.

## 1. Exported Functions

1. Extension entrypoints **MUST** be declared with `[<export>] let`.
2. Only exported functions are considered extension entrypoints.
3. Descriptor entries **MUST** reference exported function names only.

## 2. Function Signature Contract

For every exported extension function:

1. The first parameter **MUST** be named `context`.
2. The first parameter **MUST** represent the action context shape.
3. Remaining parameters **MUST** use exact target argument names (case-sensitive).
4. Parameter name matching **MUST NOT** perform case conversion, aliasing, or fallback names.
5. Exported functions **MAY** declare a return annotation (`let f ... : Type = ...`) when using FScript `0.44.0+`.

Example:

```fsharp
[<export>] let dispatch (context: {| Command: string |}) (variables: string map option) (args: string option) =
  ...
```

## 3. Argument Binding Rules

1. `context` is always supplied as the first invocation argument.
2. If a non-context parameter is missing:
   - The parameter **MUST** be typed as `option<_>` to be valid.
   - Non-optional parameters **MUST** be present.
3. Extra target keys not declared in the function signature are ignored.

## 4. Context Shape

1. Context is structurally typed.
2. Scripts **MAY** use partial record annotations for context.
3. Terrabuild currently provides fields compatible with `ActionContext` (for example `Command`, `Directory`, `Debug`, `CI`, `Hash`, `Batch`).

Canonical host shape:

```fsharp
type BatchContext =
    { Hash: string
      TempDir: string
      ProjectPaths: string list }

type ActionContext =
    { Debug: bool
      CI: bool
      Command: string
      Hash: string
      Directory: string
      Batch: BatchContext option }
```

Partial annotation is valid and preferred when only a subset is needed:

```fsharp
[<export>] let dispatch (context: {| Command: string |}) ...
```

4. FScript filesystem externs are sandboxed to the workspace root directory.
5. Terrabuild configures FScript denied-path globs from `workspace.deny` when specified.
6. If `workspace.deny` is not specified, Terrabuild uses the default deny list: `[ ".git" ]`.

### Layout and indentation rules

Examples in this document follow parser-safe FScript layout:

1. If record/map fields start on the next line, `{` **MUST** be on its own line.
2. If list items start on the next line, `[` **MUST** be on its own line.
3. Multiline record/map/list entries **SHOULD** align at a consistent indentation level.

## 5. Descriptor Contract

The script descriptor **MUST** be a map from function name to flag list.
Flags **MUST** be discriminated union cases (not strings).

Canonical form:

```fsharp
type ExportFlag =
  | Dispatch
  | Default
  | Never
  | Local
  | External
  | Remote

{ [nameof dispatch] = [Dispatch; Never] }
```

Supported flags:

- `Dispatch`
- `Default`
- cacheability: `Never`, `Local`, `External`, `Remote`

## 6. Method Resolution

1. Command resolution **MUST** use exact exported function name when present.
2. If no exact command function exists, Terrabuild **MUST** use the function flagged `dispatch` when present.
3. Default metadata resolution **MUST** use the function flagged `default` when present.

## 7. Return Contract

1. Command handlers **MUST** return a command result record with `Batchable` and `Operations`.
2. Default handlers **MUST** return a project information record.

Expected extension-facing shapes:

```fsharp
type ShellOperation =
    { Command: string
      Arguments: string
      ErrorLevel: int }

type ShellOperations = ShellOperation list

type CommandResult =
    { Batchable: bool
      Operations: ShellOperations }

type ProjectInfo =
    { Id: string option
      Outputs: string list
      Dependencies: string list }
```

`Outputs` and `Dependencies` represent unique path sets semantically.
Extensions **SHOULD** avoid duplicates.

Command handler example:

```fsharp
{ Batchable = false
  Operations = [ { Command = "make"; Arguments = "build"; ErrorLevel = 0 } ] }
```

Default handler example:

```fsharp
{ Id = None; Outputs = []; Dependencies = [] }
```

## 8. Built-In Script Contract Examples

- `shell.fss`: `dispatch (context) (args)`
- `gradle.fss`: `defaults (context)`, `dispatch (context) (args)`, `build (context) (configuration) (args)`
- `make.fss`: `dispatch (context) (variables) (args)`
- `dotnet.fss`: `defaults (context)`, `dispatch (context) (args)`, `restore/build/publish/test (context) ...`
- `docker.fss`: `dispatch (context) (args)`, `build (context) ...`, `push (context) ...`
- `npx.fss`: `run (context) (package) (args)`
- `playwright.fss`: `test (context) (browser) (project) (args)`
- `sentry.fss`: `sourcemaps (context) (project) (path)`
- `openapi.fss`: `generate (context) (generator) (input) (output) (properties) (args)`
- `terraform.fss`: `defaults (context)`, `dispatch (context) (args)`, `init/validate/select/plan/apply/destroy (context) ...`
- `null.fss`: exported handlers follow the same `context`-first rule

## 9. Protocol Evolution

Any protocol change **MUST** be introduced in this document before implementation.

## 10. Extension Template

Copy/paste starter template:

```fsharp
import "_protocol.fss" as Protocol
import "_helpers.fss" as Helpers

// Optional metadata entrypoint (flagged "default")
[<export>] let defaults (context: Protocol.ActionContext) : Protocol.ProjectInfo =
    { Id = None; Outputs = []; Dependencies = [] }

// Generic command fallback entrypoint (flagged "dispatch")
[<export>] let dispatch (context: Protocol.ActionContext) (args: string option) : Protocol.CommandResult =
    let command =
        ""
        |> Helpers.append_part context.Command
        |> Helpers.append_part (Helpers.with_args args)

    { Batchable = false
      Operations = [ { Command = "your-tool"; Arguments = command; ErrorLevel = 0 } ] }

// Specific command entrypoint example
[<export>] let build (context: Protocol.ActionContext) (configuration: string option) (args: string option) : Protocol.CommandResult =
    let configuration = configuration |> Option.defaultValue "Debug"
    let command =
        ""
        |> Helpers.append_part "build"
        |> Helpers.append_part $"--configuration {configuration}"
        |> Helpers.append_part (Helpers.with_args args)

    { Batchable = false
      Operations = [ { Command = "your-tool"; Arguments = command; ErrorLevel = 0 } ] }

// Script descriptor: exported function name -> flag list
{ [nameof defaults] = [Default]
  [nameof dispatch] = [Dispatch; Never]
  [nameof build] = [Remote] }
```

Template usage rules:

1. `context` **MUST** be the first parameter of every exported function.
2. Context field names **MUST** use exact PascalCase protocol names (`Command`, `Directory`, `Batch`, ...).
3. Shared protocol and helper definitions are provided in `Scripts/_protocol.fss` and `Scripts/_helpers.fss`; extension scripts **SHOULD** import them as `Protocol` and `Helpers`.
4. Imported symbols are available through aliases only, so scripts **MUST** use `Protocol.*` and `Helpers.*` when those aliases are chosen.
5. Non-context target arguments **SHOULD** use `option` when they may be omitted by target configuration.
6. Descriptor keys **MUST** reference exported function names (prefer `nameof`).
7. Descriptor flags **MUST** be selected from: `Dispatch`, `Default`, `Never`, `Local`, `External`, `Remote`.

Parameter XML documentation attributes:

1. `<param name="...">` **MUST** define a parameter name.
2. `default` controls requiredness in generated docs:
   - not provided: parameter is required
   - `default=""`: parameter is optional
   - `default="..."`: parameter is optional with a documented default value
3. When `default` is set to a non-empty value, DocGen appends `Default value is <value>.` to the generated argument description.
