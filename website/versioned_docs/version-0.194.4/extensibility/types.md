---
title: Types

---

This page describes the extension-facing protocol shapes used by scripted extensions.

## ExtensionContext

`ExtensionContext` is passed to handlers flagged as `default`.

```fsharp
type ExtensionContext = {
  Debug: bool
  Directory: string
  CI: bool
}
```

## ActionContext

`ActionContext` is passed as the first argument (`context`) for command handlers.

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
  Directory: string
  Batch: BatchContext option
}
```

`ActionContext` is structurally matched, so handlers can request only needed fields:

```fsharp
export let dispatch (context: { Command: string }) (args: string option) = ...
```

## ProjectInfo

Returned by handlers flagged as `default`.

```fsharp
type ProjectInfo = {
  Id: string option
  Outputs: string list
  Dependencies: string list
}
```

`Outputs` and `Dependencies` represent unique path sets semantically.

## ShellOperation

Returned by command handlers.

```fsharp
type ShellOperation = {
  Command: string
  Arguments: string
  ErrorLevel: int
}

type ShellOperations = ShellOperation list
```

## Descriptor Flags

A scripted extension returns a descriptor map at top-level:

```fsharp
type ExportFlag =
  | Dispatch
  | Default
  | Batchable
  | Never
  | Local
  | External
  | Remote

{ [nameof dispatch] = [Dispatch; Never] }
```

Supported flags:
- `Dispatch`
- `Default`
- `Batchable`
- `Never`
- `Local`
- `External`
- `Remote`
