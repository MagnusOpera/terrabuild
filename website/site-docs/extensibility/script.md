---
title: FScript extensions
---

Terrabuild custom extensions are FScript (`.fss`) programs. FScript provides the language; Terrabuild provides a small host protocol that binds target arguments to exported functions and turns their results into executable operations.

:::info FScript language documentation
If records, options, pipelines, pattern matching, or `[<export>]` are unfamiliar, begin with the [FScript tutorial](https://fscript.magnusopera.io/manual/0.78.1/learn/quickstart). The [versioned FScript manual](https://fscript.magnusopera.io/manual/0.78.1/) documents the language embedded by Terrabuild.
:::

Legacy compiled F# scripts such as `.fsx` are not supported.

## Your first extension

Create a script inside the workspace:

```fsharp {filename="tools/extensions/npm-ci.fss"}
type ShellOperation =
  { Command: string
    Arguments: string
    ErrorLevel: int }

type CommandResult =
  { Batchable: bool
    Operations: ShellOperation list }

[<export>] let install (context: {| Directory: string |}) (args: string option) : CommandResult =
  let arguments = args |> Option.defaultValue ""
  let command =
    match arguments with
    | "" -> "ci"
    | value -> $"ci {value}"

  { Batchable = false
    Operations =
      [ { Command = "npm"
          Arguments = command
          ErrorLevel = 0 } ] }

type ExportFlag =
  | Dispatch
  | Default
  | Never
  | Local
  | External
  | Remote

{ [nameof install] = [Local] }
```

Register it in the workspace:

```hcl {filename="WORKSPACE"}
extension npm_ci {
  script = "tools/extensions/npm-ci.fss"
}
```

Then call the exported function as an action:

```hcl {filename="PROJECT"}
target install {
  npm_ci install { args = "--ignore-scripts" }
}
```

The action name `install` resolves to the exported FScript function named `install`. Terrabuild supplies `context`; the target supplies `args`.

## Handler binding

Every extension entrypoint follows the same rules:

1. Declare it with `[<export>] let`.
2. Name its first parameter `context`.
3. Name remaining parameters exactly like target arguments; matching is case-sensitive.
4. Use `option<_>` for arguments that callers may omit.
5. Return a [`CommandResult`](./types#commandresult) from command handlers.

Context records are structurally typed. Request only the fields the handler uses:

```fsharp
[<export>] let dispatch (context: {| Command: string |}) (args: string option) =
  // ...
```

An omitted optional parameter becomes `None`. An omitted required parameter is a configuration error. Extra target arguments that are not present in the function signature are ignored.

## Exact actions and dispatch

Terrabuild first looks for an exported function whose name matches the requested action. If none exists, it uses the single function marked `Dispatch`.

```fsharp
[<export>] let dispatch (context: {| Command: string |}) (args: string option) : CommandResult =
  let arguments = args |> Option.defaultValue ""
  { Batchable = false
    Operations =
      [ { Command = "your-tool"
          Arguments = $"{context.Command} {arguments}"
          ErrorLevel = 0 } ] }

{ [nameof dispatch] = [Dispatch; Never] }
```

With this fallback, `my_extension lint { }` invokes `dispatch` with `context.Command = "lint"`. A script may define at most one dispatch handler.

## The descriptor

The script's final expression must be a map from exported function names to lists of discriminated-union flags. Flags are union cases, not strings.

```fsharp
type ExportFlag =
  | Dispatch
  | Default
  | Never
  | Local
  | External
  | Remote

{ [nameof defaults] = [Default]
  [nameof dispatch] = [Dispatch; Never]
  [nameof build] = [Remote] }
```

`Dispatch` and `Default` select special handlers. Every command handler also needs exactly one cacheability behavior:

| Flag | Artifact behavior |
|------|-------------------|
| `Never` | Do not cache outputs. |
| `Local` | Store outputs in the local workspace cache. |
| `Remote` | Use managed caching, including Insights when connected. |
| `External` | The command manages artifacts externally; Terrabuild stores its summary. |

When a target runs several commands, the cacheability of the **last command** is
the target's default artifact mode. This is intentional: the final command is
treated as the owner of the target's resulting artifacts. Reordering commands can
therefore change whether Terrabuild stores files locally, manages them remotely,
keeps only an external execution summary, or does not cache them. Set the target's
`artifacts` attribute when ownership should not be inferred from command order.

Batching is not a descriptor flag. Each invocation reports whether it supports batching through `CommandResult.Batchable`.

## Optional project defaults

A handler marked `Default` can discover project identity, outputs, and dependencies while Terrabuild constructs the project graph:

```fsharp
type DependencyResolution =
  | Path
  | Scope

type ProjectInfo =
  { Id: string option
    DependencyResolution: DependencyResolution option
    Outputs: string list
    Dependencies: string list }

[<export>] let defaults (context: {| Directory: string |}) : ProjectInfo =
  { Id = None
    DependencyResolution = Some Path
    Outputs = ["dist/**"]
    Dependencies = [] }

{ [nameof defaults] = [Default] }
```

A script may define at most one default handler. See [`ProjectInfo`](./types#projectinfo) for identity and dependency-resolution semantics.

## Batching

Set `Batchable = true` only when the handler can produce one correct operation for the supplied batch. In that case, request `Batch` from the action context and use its project paths, commands, hash, or temporary directory.

Returning `Batchable = false` keeps the action as an individual operation. Terrabuild still applies its normal graph and phase rules.

## Local and remote scripts

A local script path is resolved from the directory containing the `WORKSPACE` or `PROJECT` file that declares it and must remain inside the workspace:

```hcl
extension my_extension {
  script = "tools/extensions/my-extension.fss"
}
```

Remote scripts must use HTTPS:

```hcl
extension my_extension {
  script = "https://example.org/terrabuild/my-extension.fss"
}
```

Imports in local scripts are resolved to workspace files. Imports in remote scripts are resolved relative to the script URL. Terrabuild confines filesystem host functions to the workspace and applies `workspace.deny`, but returned shell operations execute with the run's authority. Review remote extension code as carefully as other automation code.

## Where to continue

- [Protocol Types](./types) lists every context and result shape.
- [Host Functions](./functions) lists the functions Terrabuild exposes to scripts.
- [Container](./container) explains how an extension runs its returned operations in Docker or Podman.
- The normative protocol is maintained in [`docs/architecture/fscript-extension-protocol.md`](https://github.com/MagnusOpera/terrabuild/blob/main/docs/architecture/fscript-extension-protocol.md).
