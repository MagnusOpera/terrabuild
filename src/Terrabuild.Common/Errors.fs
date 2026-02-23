module Errors
open System
open System.IO
open System.Runtime.ExceptionServices

[<RequireQualifiedAccess>]
type ErrorArea =
    | Parse
    | Type
    | Symbol
    | InvalidArg
    | External
    | Bug
    | Auth

type SourceLocation =
    { File: string option
      StartLine: int
      StartColumn: int
      EndLine: int
      EndColumn: int }
with
    member x.Format() =
        let file =
            x.File
            |> Option.filter (String.IsNullOrWhiteSpace >> not)
            |> Option.map (fun file ->
                try
                    let cwd = Environment.CurrentDirectory
                    let relative =
                        if Path.IsPathRooted(file) then
                            let absoluteWithoutSlash = FS.workspaceRelative "/" "/" file
                            let absolute = "/" + absoluteWithoutSlash
                            FS.relativePath cwd absolute
                        else
                            FS.workspaceRelative cwd cwd file
                    if relative.StartsWith("..") then file else relative
                with
                | _ -> file)
            |> Option.defaultValue "<unknown>"
        $"{file}:{x.StartLine}:{x.StartColumn}"

type TerrabuildException(msg, area, ?innerException: Exception, ?location: SourceLocation) =
    inherit Exception(msg, innerException |> Option.toObj)
    member _.Area: ErrorArea = area
    member val Location: SourceLocation option = location with get, set


let raiseInvalidArg(msg) =
    TerrabuildException(msg, ErrorArea.InvalidArg) |> raise

let forwardInvalidArg(msg, innerException) =
    TerrabuildException(msg, ErrorArea.InvalidArg, innerException) |> raise

let raiseParseError(msg) =
    TerrabuildException(msg, ErrorArea.Parse) |> raise

let forwardParseError(msg, innerException) =
    TerrabuildException(msg, ErrorArea.Parse, innerException) |> raise

let raiseTypeError(msg) =
    TerrabuildException(msg, ErrorArea.Type) |> raise

let raiseSymbolError(msg) =
    TerrabuildException(msg, ErrorArea.Symbol) |> raise

let raiseBugError(msg) =
    TerrabuildException(msg, ErrorArea.Bug) |> raise

let raiseExternalError(msg) =
    TerrabuildException(msg, ErrorArea.External) |> raise

let forwardExternalError(msg, innerException) =
    TerrabuildException(msg, ErrorArea.External, innerException) |> raise

let forwardAuthError(msg, innerException) =
    TerrabuildException(msg, ErrorArea.Auth, innerException) |> raise

let withLocation (location: SourceLocation) (ex: exn) =
    match ex with
    | :? TerrabuildException as terrabuildEx ->
        if terrabuildEx.Location.IsNone then
            terrabuildEx.Location <- Some location
        terrabuildEx :> exn
    | _ -> ex


let rec dumpKnownException (ex: Exception | null) =
    seq {
        match ex with
        | :? TerrabuildException as ex ->
            let location =
                ex.Location
                |> Option.map (fun loc -> $" ({loc.Format()})")
                |> Option.defaultValue ""
            yield $"{ex.Message}{location}"
            yield! ex.InnerException |> dumpKnownException
        | null -> ()
        | _ -> ()
    }

let getErrorArea (ex: Exception) =
    let rec getErrorArea (area: ErrorArea) (ex: Exception | null) =
        match ex with
        | :? TerrabuildException as ex -> getErrorArea ex.Area ex.InnerException
        | null -> area
        | _ -> area

    getErrorArea ErrorArea.Bug ex


let tryInvoke action =
    try
        action()
        None
    with
        exn -> ExceptionDispatchInfo.Capture(exn) |> Some
