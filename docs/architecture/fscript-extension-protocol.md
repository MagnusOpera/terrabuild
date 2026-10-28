# Terrabuild FScript Extension Protocol

This document defines the normative protocol between Terrabuild and FScript extensions.

Terrabuild supports FScript extension scripts only. Custom extension scripts **MUST** use the `.fss` file extension.

## Conformance Language

The keywords **MUST**, **MUST NOT**, **SHOULD**, and **MAY** are normative.

## 1. Exported Functions

1. Extension entrypoints **MUST** be declared with `export let`.
2. Only exported functions are considered extension entrypoints.
3. Descriptor entries **MUST** reference exported function names only.
4. Legacy compiled F# extension scripts such as `.fsx` **MUST NOT** be used.

## 2. Function Signature Contract

For every exported extension function:

1. The first parameter **MUST** be named `context`.
2. The first parameter **MUST** represent the metadata context shape for a `Default` function or the action context shape for a command.
3. Remaining parameters **MUST** use exact target argument names (case-sensitive).
4. Parameter name matching **MUST NOT** perform case conversion, aliasing, or fallback names.
5. Exported functions **MAY** declare a return annotation (`let f ... : Type = ...`) when using FScript `0.44.0+`.

Example:

```fsharp
export let dispatch (context: {| Command: string |}) (variables: string map option) (args: string option) =
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

Metadata discovery supplies `ExtensionContext` (`Debug`, `Directory`, `CI`); command execution supplies the full `ActionContext`. Default functions must not require command-only fields.

Canonical command host shape:

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

Partial annotation is valid and preferred when only a subset is needed:

```fsharp
export let dispatch (context: {| Command: string |}) ...
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
{ [nameof dispatch] = [Dispatch; Cache Never] }
```

Supported flags:

- `Dispatch`
- `Default`
- cacheability: `Cache Never`, `Cache Local`, `Cache External`, `Cache Remote`

Preservation is a discriminated-union **command output**, not a descriptor flag.
`ProvenanceEffect` has three cases: `Preserved`, `Broken`, and `Unaffected`.
Every command **MUST** return `PreserveProvenance = Preserved`, `Broken`, or
`Unaffected`. Missing fields, booleans, and option values **MUST** be rejected.
Extensions own the declaration and any computation used to obtain it. Terrabuild
**MUST NOT** inspect shell commands, arguments, ecosystem identities or lock files
to infer preservation. The declaration **MUST NOT** enter operation hash serialization.

## 6. Method Resolution

1. Command resolution **MUST** use exact exported function name when present.
2. If no exact command function exists, Terrabuild **MUST** use the function flagged `dispatch` when present.
3. Default metadata resolution **MUST** use the function flagged `default` when present.
4. Preservation **MUST** use the output of the actual invoked entrypoint, including dispatch fallback.
5. Terrabuild **MUST** carry the loaded extension origin and script identity with the declaration; Insights owns trust evaluation and report computation.

Legacy entrypoint names such as `__dispatch__` and `__defaults__` are not supported.

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

type ProvenanceEffect =
  | Preserved
  | Broken
  | Unaffected

type CommandResult =
    { Batchable: bool
      Operations: ShellOperations
      PreserveProvenance: ProvenanceEffect }

type DependencyResolution =
    | Path
    | Scope

type ProjectInfo =
    { Id: string option
      DependencyResolution: DependencyResolution option
      Outputs: string list
      Dependencies: string list }
```

`Outputs` and `Dependencies` represent unique path sets semantically.
Extensions **SHOULD** avoid duplicates.

`DependencyResolution` controls how `Dependencies` are interpreted:

- `Path`: each dependency is a relative path from the current project directory
- `Scope`: each dependency is an identifier inside the current extension scope (for example `@pnpm#name`)

If omitted, Terrabuild defaults to `Path`.

`Id` controls the canonical project identifier for the extension:

- when `Id` is provided, Terrabuild uses `<extension-scope>#<Id>`
- when `Id` is omitted, Terrabuild falls back to `workspace/path#<relative-project-path>`

This is independent from `DependencyResolution`. For example, a pnpm extension can return `Id = Some "@scope/pkg"` and `DependencyResolution = Some Scope` so the project keeps a stable canonical ID while dependencies resolve in the pnpm scope.

Command handler example:

```fsharp
{ PreserveProvenance = Broken
  Batchable = false
  Operations = [ { Command = "make"; Arguments = "build"; ErrorLevel = 0 } ] }
```

Default handler example:

```fsharp
{ Id = None; DependencyResolution = None; Outputs = []; Dependencies = [] }
```

## 8. Built-In Script Contract Examples

- `shell.fss`: `dispatch (context) (args)`
- `gradle.fss`: `defaults (context)`, `dispatch (context) (args)`, `build (context) (configuration) (args)`
- `go.fss`: `defaults (context)`, `dispatch (context) (args)`, `install/build/test (context) ...`
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

## 10. Built-In Extension Template

Terrabuild's embedded extensions use the shared protocol and helper modules in `src/Terrabuild.Extensions/Scripts`:

```fsharp
import "_helpers.fss" as Helpers

// Optional metadata entrypoint (flagged "default")
export let defaults (context: Protocol.ExtensionContext) : Protocol.ProjectInfo =
    { Id = None; DependencyResolution = None; Outputs = []; Dependencies = [] }

// Generic command fallback entrypoint (flagged "dispatch")
export let dispatch (context: Protocol.ActionContext) (args: string option) : Protocol.CommandResult =
    let command =
        ""
        |> Helpers.append_part context.Command
        |> Helpers.append_part (Helpers.with_args args)

    { Batchable = false
      PreserveProvenance = Broken
      Operations = [ { Command = "your-tool"; Arguments = command; ErrorLevel = 0 } ] }

// Specific command entrypoint example
export let build (context: Protocol.ActionContext) (configuration: string option) (args: string option) : Protocol.CommandResult =
    let configuration = configuration |> Option.defaultValue "Debug"
    let command =
        ""
        |> Helpers.append_part "build"
        |> Helpers.append_part $"--configuration {configuration}"
        |> Helpers.append_part (Helpers.with_args args)

    { Batchable = false
      PreserveProvenance = Broken
      Operations = [ { Command = "your-tool"; Arguments = command; ErrorLevel = 0 } ] }

// Script descriptor: exported function name -> flag list
{ [nameof defaults] = [Default]
  [nameof dispatch] = [Dispatch; Cache Never]
  [nameof build] = [Cache Remote] }
```

Template usage rules:

1. `context` **MUST** be the first parameter of every exported function.
2. Context field names **MUST** use exact PascalCase protocol names (`Command`, `Directory`, `Batch`, ...).
3. Terrabuild registers the CLR protocol from `Terrabuild.Scripting` for every built-in and custom extension. Scripts **MUST NOT** redeclare these record shapes. `_helpers.fss` remains an embedded script utility for built-in extensions.
4. Registered types use `Protocol.*` names without an import. Helpers imported as `Helpers` use that alias.
5. Non-context target arguments **SHOULD** use `option` when they may be omitted by target configuration.
6. Descriptor keys **MUST** reference exported function names (prefer `nameof`).
7. Descriptor flags **MUST** be selected from: `Dispatch`, `Default`, and `Cache` applied to `Never`, `Local`, `External`, or `Remote`.

Migration rules for legacy compiled extension scripts:

1. Rename custom extension files from `.fsx` to `.fss`.
2. Replace `__dispatch__` with an exported function flagged `Dispatch`.
3. Replace `__defaults__` with an exported function flagged `Default`.
4. Replace any reflection or attribute-based cacheability metadata with descriptor flags.

Parameter XML documentation attributes:

1. `<param name="...">` **MUST** define a parameter name.
2. `default` controls requiredness in generated docs:
   - not provided: parameter is required
   - `default=""`: parameter is optional
   - `default="..."`: parameter is optional with a documented default value
3. When `default` is set to a non-empty value, DocGen appends `Default value is <value>.` to the generated argument description.

## Typed invocation arguments

Terrabuild uses the FScript Language and Runtime 0.87.2 packages by default. `UseLocalFScript=true` switches to sibling project references for development; both build modes use the same argument adaptation, including FScript structural record constraints (`TRecordShape`).

The scripting bridge uses `ScriptHost.LoadedScript.ExportedFunctionSignatures` and `TypeDefs` to adapt configuration values before invoking a function. Configuration maps remain FScript maps for map parameters and become `VRecord` for declared or structural record parameters. Nested records, lists, map values, and options are adapted recursively; omitted optional record fields become `None`. Enum strings become declared payload-free union cases, and CLR union values are qualified to the matching script type. Host-managed values retain their opaque representation and cannot be manufactured from configuration maps. FScript remains responsible for validating the resulting argument and return values against the signature.

## Shared FFI environment

`ScriptingContracts` is the source of truth. Its attributed records and unions are described once by `FScriptProtocol`: six constructible records and four unions (`DependencyResolution`, `ProvenanceEffect`, `Cacheability`, `ExportFlag`). CLR sets project to script lists and reconstruct with set deduplication/order. All contexts and results remain constructible; no opaque state is introduced.

The shared registration supplies the approved standard externs while excluding `Task.spawn`, `Task.await`, and `Console.readLine`. Execution and DocGen load this same registration. Built-in contract fingerprints include a stable serialization of the registered schema and script sources, rather than a copied protocol file or assembly build identity.

`src/Terrabuild.Extensions/Scripts/fscript.toml` selects the `Terrabuild.Scripting` assembly for CLI and VS Code authoring after a Release build. It disables automatic defaults because the provider supplies the approved extern set. Loading a provider only describes contracts; it does not execute an extension. Custom authors may copy the manifest beside their scripts and adjust the assembly path. Terrabuild itself always supplies its protocol, independently of editor manifest selection.

The default build uses the FScript 0.87.2 packages, including foreign union and set projection. A sibling checkout is only needed when explicitly developing FScript through `flags=fscript` or `/p:UseLocalFScript=true`.

## Independent SDK boundary

Attributed protocol contracts and the provider use `FScript.Extensibility` from `MagnusOpera.FScript.Extensibility` 1.0.0. The SDK has its own stable unsigned assembly identity (1.0.0.0), independent of FScript product versions. Providers receive read-only SDK `HostContext` and filter its `DefaultFunctions`, preserving Terrabuild's restricted catalog without depending on Runtime implementations in the provider contract.

Terrabuild's embedding layer still references Language/Runtime and adapts the SDK registration with `ExtensionBridge.toLanguageRegistration`. The scripting assembly hosts both contracts and embedding; standalone extension assemblies can reference only the SDK. Script syntax, constructible protocol types, sets, manifests, and restricted function policy are unchanged.

Development checkouts now live under the FScript umbrella: `../FScript/FScript` for the product and `../FScript/FScript.Extensibility` for the SDK. `UseLocalFScript=true` selects product projects; the separate `UseLocalFScriptExtensibility=true` selects SDK sources. Make accepts `flags=fscript,fscript-sdk`. Normal builds and release CI use NuGet. The product package target is 0.87.2; ordinary builds restore it from NuGet without a sibling checkout.

## Interpreted execution and purity metadata

Terrabuild continues using the AST evaluator through `ScriptHost` for both loading and invocation. It does not depend on the compiler, LIR interpreter, or either backend. The shared `FScriptVersion` property pins Language and Runtime to 0.87.2 when packages are used; the extension SDK remains 1.0.0.

`FScriptProtocolProvider.FScriptPureTypes()` advertises the ten protocol records/unions as deeply immutable data contracts. Their registered fields and payloads contain only immutable values; no opaque mutable handles or effectful getters are exposed. `FScriptPureFunctions()` returns an empty sequence: approved runtime functions are not blanket-marked pure. The type list is an explicit allowlist: a newly registered type needs a separate immutability review. FScript purity is not inferred from Terrabuild cacheability or provenance flags. These optional BCL companion methods are only consumed by FScript compiler tooling and have no optimization effect on interpreted Terrabuild extensions. They do not memoize or reorder extension calls.

Extensions return typed protocol values. `Env.setExitCode` is execution-local FScript metadata and does not select Terrabuild's build exit status; Terrabuild intentionally uses value-returning `ScriptHost.invoke`. Invocation exceptions and operation `ErrorLevel` values remain the failure protocol. Result printing is controlled by the embedding host, so CLI `--no-result` is not needed here.

### Validate the published integration

```sh
make try-docs
make test
make smoke-tests
make self
```

Add `flags=fscript` to these commands when validating changes in the sibling product checkout instead of the published packages.

Local flags propagate to nested host MSBuild processes. Local self-builds select the host engine and `local` workspace configuration so sibling source projects remain accessible, and force execution because sibling source changes are not workspace cache inputs. The repository workspace permits the CLI engine selection; its default remains Docker. Normal self-builds retain the configured engine and configuration. `flags=fscript,fscript-sdk` also selects the local SDK checkout when developing the SDK itself. Package builds target the FScript 0.87.2 release; no compiler migration is needed for Terrabuild.
