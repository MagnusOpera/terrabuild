namespace Terrabuild

open Collections

module ScriptingContracts =
    [<RequireQualifiedAccess>]
    type ExtensionContext = {
        Debug: bool
        Directory: string
        CI: bool
    }

    [<RequireQualifiedAccess>]
    type DependencyResolution =
        | Path
        | Scope

    [<RequireQualifiedAccess>]
    type ProjectInfo = {
        Id: string option
        DependencyResolution: DependencyResolution option
        Outputs: Set<string>
        Dependencies: Set<string>
    }
    with
        static member Default = {
            Id = None
            DependencyResolution = Some DependencyResolution.Path
            Outputs = Set.empty
            Dependencies = Set.empty
        }

    [<RequireQualifiedAccess>]
    type BatchContext = {
        Hash: string
        TempDir: string
        ProjectPaths: string set
        BatchCommands: string list
    }

    [<RequireQualifiedAccess>]
    type ActionContext = {
        Debug: bool
        CI: bool
        Command: string
        Hash: string
        Directory: string
        Batch: BatchContext option
    }

    [<RequireQualifiedAccess>]
    type ShellOperation = {
        Command: string
        Arguments: string
        ErrorLevel: int
    }

    type ShellOperations = ShellOperation list

    [<RequireQualifiedAccess>]
    type CommandResult = {
        Batchable: bool
        Operations: ShellOperations
    }

    [<RequireQualifiedAccess>]
    type Cacheability =
        | Never
        | Local
        | External
        | Remote

    [<RequireQualifiedAccess>]
    type ExportFlag =
        | Dispatch
        | Default
        | Cache of Cacheability

    type ScriptDescriptor = Map<string, ExportFlag list>
