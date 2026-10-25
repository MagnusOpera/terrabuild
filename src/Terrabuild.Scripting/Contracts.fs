namespace Terrabuild

open Collections
open FScript.Extensibility

module ScriptingContracts =
    [<RequireQualifiedAccess; FScriptHostType("Protocol.ExtensionContext", ScriptConstructible = true)>]
    type ExtensionContext = {
        [<FScriptVisible>] Debug: bool
        [<FScriptVisible>] Directory: string
        [<FScriptVisible>] CI: bool
    }

    [<RequireQualifiedAccess; FScriptHostType("Protocol.DependencyResolution", ScriptConstructible = true)>]
    type DependencyResolution =
        | Path
        | Scope

    [<RequireQualifiedAccess; FScriptHostType("Protocol.ProjectInfo", ScriptConstructible = true)>]
    type ProjectInfo = {
        [<FScriptVisible>] Id: string option
        [<FScriptVisible>] DependencyResolution: DependencyResolution option
        [<FScriptVisible>] Outputs: Set<string>
        [<FScriptVisible>] Dependencies: Set<string>
    }
    with
        static member Default = {
            Id = None
            DependencyResolution = Some DependencyResolution.Path
            Outputs = Set.empty
            Dependencies = Set.empty
        }

    [<RequireQualifiedAccess; FScriptHostType("Protocol.BatchContext", ScriptConstructible = true)>]
    type BatchContext = {
        [<FScriptVisible>] Hash: string
        [<FScriptVisible>] TempDir: string
        [<FScriptVisible>] ProjectPaths: string set
        [<FScriptVisible>] BatchCommands: string list
    }

    [<RequireQualifiedAccess; FScriptHostType("Protocol.ActionContext", ScriptConstructible = true)>]
    type ActionContext = {
        [<FScriptVisible>] Debug: bool
        [<FScriptVisible>] CI: bool
        [<FScriptVisible>] Command: string
        [<FScriptVisible>] Hash: string
        [<FScriptVisible>] Directory: string
        [<FScriptVisible>] Batch: BatchContext option
    }

    [<RequireQualifiedAccess; FScriptHostType("Protocol.ShellOperation", ScriptConstructible = true)>]
    type ShellOperation = {
        [<FScriptVisible>] Command: string
        [<FScriptVisible>] Arguments: string
        [<FScriptVisible>] ErrorLevel: int
    }

    type ShellOperations = ShellOperation list

    [<RequireQualifiedAccess; FScriptHostType("Protocol.ProvenanceEffect", ScriptConstructible = true)>]
    type ProvenanceEffect =
        | Preserved
        | Broken
        | Unaffected

    [<RequireQualifiedAccess; FScriptHostType("Protocol.CommandResult", ScriptConstructible = true)>]
    type CommandResult = {
        [<FScriptVisible>] Batchable: bool
        [<FScriptVisible>] Operations: ShellOperations
        [<FScriptVisible>] PreserveProvenance: ProvenanceEffect
    }

    [<RequireQualifiedAccess; FScriptHostType("Protocol.Cacheability", ScriptConstructible = true)>]
    type Cacheability =
        | Never
        | Local
        | External
        | Remote

    [<RequireQualifiedAccess; FScriptHostType("Protocol.ExportFlag", ScriptConstructible = true)>]
    type ExportFlag =
        | Dispatch
        | Default
        | Cache of Cacheability

    type ScriptDescriptor = Map<string, ExportFlag list>
