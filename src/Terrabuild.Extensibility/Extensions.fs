module Terrabuild.Extensibility
open System
open System.Text

[<RequireQualifiedAccess>]
type ExtensionContext = {
    Debug: bool
    Directory: string
    CI: bool
}

[<RequireQualifiedAccess>]
type ProjectInfo = {
    Outputs: Set<string>
    Ignores: Set<string>
    Dependencies: Set<string>
    Includes: Set<string>
}
with
    static member Default = {
        Outputs = Set.empty
        Ignores = Set.empty
        Dependencies = Set.empty
        Includes = Set [ "**/*" ]
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

type ShellOperations = ShellOperation list

[<RequireQualifiedAccess>]
type Cacheability =
    | Never
    | Local
    | Remote
    | Ephemeral

let shellOp(cmd, args) = 
    { ShellOperation.Command = cmd
      ShellOperation.Arguments = args }

[<AbstractClass>]
[<AttributeUsage(AttributeTargets.Method, AllowMultiple = false)>]
type CacheableAttribute(cacheability: Cacheability) =
    inherit Attribute()
    member _.Cacheability = cacheability

type EphemeralCacheAttribute() =
    inherit CacheableAttribute(Cacheability.Ephemeral)

type RemoteCacheAttribute() =
    inherit CacheableAttribute(Cacheability.Remote)

type LocalCacheAttribute() =
    inherit CacheableAttribute(Cacheability.Local)

type NoCacheAttribute() =
    inherit CacheableAttribute(Cacheability.Never)
