---
title: Protocol Types
---

These are the FScript shapes exchanged with Terrabuild. They are ordinary FScript records and discriminated unions; see the [FScript type-system documentation](https://magnusopera.github.io/FScript/manual/0.75.0/language/type-system) for the language rules.

## ExtensionContext

Terrabuild supplies this context to the handler marked `Default` while discovering project metadata:

```fsharp
type ExtensionContext =
  { Debug: bool
    Directory: string
    CI: bool }
```

## ActionContext

Terrabuild supplies this context to command handlers:

```fsharp
type BatchContext =
  { Hash: string
    TempDir: string
    ProjectPaths: string list
    BatchCommands: string list }

type ActionContext =
  { Debug: bool
    CI: bool
    Command: string
    Hash: string
    Directory: string
    Batch: BatchContext option }
```

- `Command` is the requested target action.
- `Hash` identifies the current execution context.
- `Directory` is the project directory.
- `Batch` is `Some` during batch resolution and `None` for an individual action.

Contexts are structurally matched, so handlers should request only the fields they use:

```fsharp
[<export>] let dispatch (context: {| Command: string |}) (args: string option) =
  // ...
```

## ProjectInfo

The handler marked `Default` returns project metadata:

```fsharp
type DependencyResolution =
  | Path
  | Scope

type ProjectInfo =
  { Id: string option
    DependencyResolution: DependencyResolution option
    Outputs: string list
    Dependencies: string list }
```

- `Id` supplies a canonical identity inside the extension scope. When absent, Terrabuild uses the workspace-relative project path.
- `DependencyResolution = Some Path` interprets dependencies as paths relative to the current project directory.
- `DependencyResolution = Some Scope` interprets dependencies as identifiers in the current extension scope.
- `DependencyResolution = None` defaults to `Path`.
- `Outputs` contributes default artifact patterns.
- `Dependencies` contributes discovered project dependencies.

Outputs and dependencies are sets semantically; avoid returning duplicates.

## ShellOperation

Each operation describes one process Terrabuild should execute:

```fsharp
type ShellOperation =
  { Command: string
    Arguments: string
    ErrorLevel: int }
```

`ErrorLevel` is the highest accepted exit code. Operations execute in list order.

## CommandResult

Every command handler returns:

```fsharp
type CommandResult =
  { Batchable: bool
    Operations: ShellOperation list }
```

- `Batchable = true` means the returned operation is valid for the supplied batch context.
- `Batchable = false` requires individual execution.
- `Operations` may contain one or more ordered shell operations.

Batchability belongs to the result, not the descriptor.

## Descriptor Flags

The script's final value maps exported function names to these flags:

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

- `Dispatch` selects the fallback command handler.
- `Default` selects the project-metadata handler.
- `Never`, `Local`, `External`, and `Remote` specify command cacheability.

Only one function may be marked `Dispatch`, and only one may be marked `Default`. Descriptor keys must name functions declared with `[<export>]`.
