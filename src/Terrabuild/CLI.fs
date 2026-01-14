module CLI
open Argu

[<RequireQualifiedAccess>]
type Engine =
    | Docker
    | Podman
    | None

[<RequireQualifiedAccess>]
type ScaffoldArgs =
    | [<Unique; AltCommandLine("-w")>] Workspace of path:string
    | [<Unique>] Force
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Workspace _ -> "Root of workspace. If not specified, current directory is used."
            | Force -> "Default behavior is to not overwrite existing WORKSPACE or PROJECT file. This can be forced."

[<RequireQualifiedAccess>]
type LogsArgs =
    | [<ExactlyOnce; MainCommand; First>] Target of target:string list
    | [<Unique; AltCommandLine("-w")>] Workspace of path:string
    | [<Unique; AltCommandLine("-c")>] Configuration of name:string
    | [<Unique; AltCommandLine("-e")>] Environment of name:string
    | [<EqualsAssignment; AltCommandLine("-v")>] Variable of variable:string * value:string
    | [<Unique; AltCommandLine("-l")>] Label of labels:string list
    | [<Unique; AltCommandLine("-t")>] Type of types:string list
    | [<Unique; AltCommandLine("-p")>] Project of projects:string list
    | [<Unique>] Local_Only
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Target _ -> "Specify build target."
            | Workspace _ -> "Root of workspace. If not specified, current directory is used."
            | Configuration _ -> "Configuration to use."
            | Environment _ -> "Environment to use."
            | Variable _ -> "Set variable."
            | Label _-> "Select projects based on labels."
            | Type _-> "Select projects based on extension types."
            | Project _ -> "Select projets base on id."
            | Local_Only -> "Use local cache only."

[<RequireQualifiedAccess>]
type RunArgs =
    | [<ExactlyOnce; MainCommand; First>] Target of target:string list
    | [<Unique; AltCommandLine("-w")>] Workspace of path:string
    | [<Unique; AltCommandLine("-c")>] Configuration of name:string
    | [<Unique; AltCommandLine("-e")>] Environment of name:string
    | [<EqualsAssignment; AltCommandLine("-v")>] Variable of variable:string * value:string
    | [<Unique; AltCommandLine("-l")>] Label of labels:string list
    | [<Unique; AltCommandLine("-t")>] Type of types:string list
    | [<Unique; AltCommandLine("-p")>] Project of projects:string list
    | [<Unique; AltCommandLine("-f")>] Force
    | [<Unique; AltCommandLine("-r")>] Retry
    | [<Unique>] Parallel of max:int
    | [<Unique>] Local_Only
    | [<Unique>] Note of note:string
    | [<Unique>] Tag of tag:string
    | [<Unique>] Engine of engine:Engine
    | [<Unique; Inherit>] What_If
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Target _ -> "Specify build target."
            | Workspace _ -> "Root of workspace. If not specified, current directory is used."
            | Configuration _ -> "Configuration to use."
            | Environment _ -> "Environment to use."
            | Variable _ -> "Set variable."
            | Label _ -> "Select projects based on labels."
            | Type _-> "Select projects based on extension types."
            | Project _ -> "Select projets base on id."
            | Force -> "Ignore cache when building target."
            | Retry -> "Retry failed task."
            | Parallel _ -> "Max parallel build concurrency (default to number of processors)."
            | Local_Only -> "Use local cache only."
            | Note _ -> "Note for the build."
            | Tag _ -> "Tag for build."
            | Engine _ -> "Container engine to use (docker, podman or none)."
            | What_If -> "Prepare the action but do not apply."

[<RequireQualifiedAccess>]
type ServeArgs =
    | [<Unique; AltCommandLine("-w")>] Workspace of path:string
    | [<Unique; AltCommandLine("-c")>] Configuration of name:string
    | [<Unique; AltCommandLine("-e")>] Environment of name:string
    | [<EqualsAssignment; AltCommandLine("-v")>] Variable of variable:string * value:string
    | [<Unique; AltCommandLine("-l")>] Label of labels:string list
    | [<Unique; AltCommandLine("-t")>] Type of types:string list
    | [<Unique; AltCommandLine("-p")>] Project of projects:string list
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Workspace _ -> "Root of workspace. If not specified, current directory is used."
            | Configuration _ -> "Configuration to use."
            | Environment _ -> "Environment to use."
            | Variable _ -> "Set variable."
            | Label _ -> "Select projects based on labels."
            | Type _-> "Select projects based on extension types."
            | Project _ -> "Select projets base on id."

[<RequireQualifiedAccess>]
type GraphArgs =
    | [<Unique; AltCommandLine("-w")>] Workspace of path:string
    | [<Unique>] No_Open
    | [<Unique; AltCommandLine("-p")>] Port of port:int
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Workspace _ -> "Root of workspace. If not specified, current directory is used."
            | No_Open -> "Do not open the browser automatically."
            | Port _ -> "Port to bind the graph API and UI server."


[<RequireQualifiedAccess>]
type ClearArgs =
    | [<Unique>] Cache
    | [<Unique>] Home
    | [<Unique>] All
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Cache -> "Clear build cache."
            | Home -> "Clear home cache."
            | All -> "Clear all caches."

[<RequireQualifiedAccess>]
type LoginArgs =
    | [<ExactlyOnce>] Workspace of id:string
    | [<ExactlyOnce>] Token of token:string
    | [<ExactlyOnce>] MasterKey of masterKey:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Workspace _ -> "Workspace Id to connect to"
            | Token _ -> "Token to connect to space"
            | MasterKey _ -> "Master key to encrypt artifacts"

[<RequireQualifiedAccess>]
type LogoutArgs =
    | [<ExactlyOnce>] Space of space:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Space _ -> "Slug of space to remove"

[<RequireQualifiedAccess>]
type TerrabuildArgs =
    | [<CliPrefix(CliPrefix.None)>] Scaffold of ParseResults<ScaffoldArgs>
    | [<CliPrefix(CliPrefix.None)>] Logs of ParseResults<LogsArgs>
    | [<CliPrefix(CliPrefix.None)>] Run of ParseResults<RunArgs>
    | [<CliPrefix(CliPrefix.None)>] Serve of ParseResults<ServeArgs>
    | [<CliPrefix(CliPrefix.None)>] Graph of ParseResults<GraphArgs>
    | [<CliPrefix(CliPrefix.None)>] Clear of ParseResults<ClearArgs>
    | [<CliPrefix(CliPrefix.None)>] Login of ParseResults<LoginArgs>
    | [<CliPrefix(CliPrefix.None)>] Logout of ParseResults<LogoutArgs>
    | [<CliPrefix(CliPrefix.None)>] Version
    | [<Hidden; Unique; Inherit>] Log
    | [<Hidden; Unique; Inherit>] Debug
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Scaffold _ -> "Scaffold workspace."
            | Logs _ -> "dump logs."
            | Run _ -> "Run specified targets."
            | Serve _ -> "Serve specified targets."
            | Graph _ -> "Visualize and build graph in a web UI."
            | Clear _ -> "Clear specified caches."
            | Login _ -> "Connect to backend."
            | Logout _ -> "Disconnect from backend."
            | Version -> "Show current Terrabuild version."
            | Log -> "Enable logging."
            | Debug -> "Enable debug logs."
