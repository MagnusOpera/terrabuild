namespace Terrabuild

open System
open Collections

module ScriptingContracts =
    [<RequireQualifiedAccess>]
    type ExtensionContext = {
        Debug: bool
        Directory: string
        CI: bool
    }

    [<RequireQualifiedAccess>]
    type ProjectInfo = {
        Id: string option
        Outputs: Set<string>
        Dependencies: Set<string>
    }
    with
        static member Default = {
            Id = None
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

    [<AbstractClass>]
    [<AttributeUsage(AttributeTargets.Method, AllowMultiple = false)>]
    type CacheableAttribute(cacheability: Cacheability) =
        inherit Attribute()
        member _.Cacheability = cacheability

    type ExternalCacheAttribute() =
        inherit CacheableAttribute(Cacheability.External)

    type RemoteCacheAttribute() =
        inherit CacheableAttribute(Cacheability.Remote)

    type LocalCacheAttribute() =
        inherit CacheableAttribute(Cacheability.Local)

    type NoCacheAttribute() =
        inherit CacheableAttribute(Cacheability.Never)
