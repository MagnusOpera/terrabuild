---
title: Script

---

Terrabuild custom extensions are implemented with FScript (`.fss`).

Legacy compiled F# scripts such as `.fsx` are not supported.

## Configure a Scripted Extension

### Local script (workspace-confined)

```hcl {filename="WORKSPACE"}
extension npm_ci {
  script = "tools/extensions/npm-ci.fss"
}
```

The script path is resolved from the workspace and must remain inside the workspace.

### Remote script (HTTPS)

```hcl {filename="WORKSPACE"}
extension npm_ci {
  script = "https://example.org/terrabuild/npm-ci.fss"
}
```

Only HTTPS URLs are accepted.

## Use extension actions

```hcl {filename="PROJECT"}
target build {
  npm_ci install { args = "--frozen-lockfile" }
}
```

Action names map to exported function names (or to the handler flagged `Dispatch`).

## Script Contract

Exported handlers must follow these rules:
1. Use `export let`.
2. First parameter must be `context`.
3. Additional parameter names are matched exactly (case-sensitive) with target arguments.
4. Omitted non-context arguments must be typed as `option<_>`.

Example:

```fsharp
export let dispatch (context: { Command: string }) (args: string option) =
  let command =
    match args with
    | Some value -> $"{context.Command} {value}"
    | None -> context.Command

  [{ Command = "npm"; Arguments = command; ErrorLevel = 0 }]
```

## Descriptor flags

Each script returns a descriptor map (`function -> flags`) at top-level.
Flags are discriminated union cases (not strings).

```fsharp
type ExportFlag =
  | Dispatch
  | Default
  | Batchable
  | Never
  | Local
  | External
  | Remote

{
  [nameof defaults] = [Default]
  [nameof dispatch] = [Dispatch; Never]
  [nameof build] = [Batchable; Remote]
}
```

Supported flags:
- `Dispatch`
- `Default`
- `Batchable`
- cacheability: `Never`, `Local`, `External`, `Remote`

## Extension Template

Copy/paste starter:

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

type ProjectInfo = {
  Id: string option
  Outputs: string list
  Dependencies: string list
}

type ShellOperation = {
  Command: string
  Arguments: string
  ErrorLevel: int
}

type ShellOperations = ShellOperation list

type ExportFlag =
  | Dispatch
  | Default
  | Batchable
  | Never
  | Local
  | External
  | Remote

let with_args args =
  args |> Option.defaultValue ""

export let defaults (context: ActionContext) : ProjectInfo =
  { Id = None; Outputs = []; Dependencies = [] }

export let dispatch (context: ActionContext) (args: string option) : ShellOperations =
  let command =
    match with_args args with
    | "" -> context.Command
    | value -> $"{context.Command} {value}"

  [{ Command = "your-tool"; Arguments = command; ErrorLevel = 0 }]

export let build (context: ActionContext) (configuration: string option) (args: string option) : ShellOperations =
  let config = configuration |> Option.defaultValue "Debug"
  let cmdArgs =
    match with_args args with
    | "" -> $"build --configuration {config}"
    | value -> $"build --configuration {config} {value}"

  [{ Command = "your-tool"; Arguments = cmdArgs; ErrorLevel = 0 }]

{
  [nameof defaults] = [Default]
  [nameof dispatch] = [Dispatch; Never]
  [nameof build] = [Remote]
}
```

## Notes

- Context types are structural: handlers can request only required fields.
- Filesystem external functions are sandboxed to workspace scope and can be further denied with `workspace.deny`.
- `workspace.deny` accepts glob paths and defaults to `[ ".git" ]` when omitted.
- Security bypass attempts (for example traversal through denied paths) are blocked by the runtime sandbox.
- For protocol details, see `src/Terrabuild/Scripts/EXTENSION-PROTOCOL.md` in the Terrabuild repository.
- FScript repository: https://github.com/MagnusOpera/FScript
