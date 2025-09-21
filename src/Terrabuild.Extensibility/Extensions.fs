module Terrabuild.Extensibility
open System

[<RequireQualifiedAccess>]
type ExtensionContext = {
    Debug: bool
    Directory: string
    CI: bool
}

[<RequireQualifiedAccess>]
type ProjectInfo = {
    Outputs: Set<string>
    Dependencies: Set<string>
}
with
    static member Default = {
        Outputs = Set.empty
        Dependencies = Set.empty
    }

[<RequireQualifiedAccess>]
type ActionContext = {
    Debug: bool
    CI: bool
    Command: string
    Hash: string
}

[<RequireQualifiedAccess>]
type ShellOperation = {
    Command: string
    Arguments: string
}

[<RequireQualifiedAccess>]
type BatchContext = {
    Hash: string
    Operation: ShellOperation
    ProjectPaths: string list
}

type ShellOperations = ShellOperation list

[<RequireQualifiedAccess>]
type Cacheability =
    | Never
    | Local
    | Remote

let shellOp(cmd, args) = 
    { ShellOperation.Command = cmd
      ShellOperation.Arguments = args }

[<AttributeUsage(AttributeTargets.Method, AllowMultiple = false)>]
type BatchableAttribute() =
    inherit Attribute()

[<AbstractClass>]
[<AttributeUsage(AttributeTargets.Method, AllowMultiple = false)>]
type CacheableAttribute(cacheability: Cacheability) =
    inherit Attribute()
    member _.Cacheability = cacheability

type RemoteCacheAttribute() =
    inherit CacheableAttribute(Cacheability.Remote)

type LocalCacheAttribute() =
    inherit CacheableAttribute(Cacheability.Local)

type NoCacheAttribute() =
    inherit CacheableAttribute(Cacheability.Never)
