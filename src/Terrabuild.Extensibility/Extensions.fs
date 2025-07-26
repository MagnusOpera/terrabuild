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

[<Flags>]
type Cacheability =
    | Never     = 0b00000000
    | Local     = 0b00000001
    | Remote    = 0b00000010
    | Always    = 0b00000011 // Local + Remote
    | Ephemeral = 0b00001000

type ShellOperations = ShellOperation list

[<RequireQualifiedAccess>]
type ActionExecutionRequest = {
    Cache: Cacheability
    Operations: ShellOperations
}


let shellOp(cmd, args) = 
    { ShellOperation.Command = cmd
      ShellOperation.Arguments = args }

let execRequest(cache, ops) =
    { ActionExecutionRequest.Cache = cache 
      ActionExecutionRequest.Operations = ops }
