module Errors
open System
open System.Runtime.ExceptionServices
open System.Collections.Generic
open System.Threading

[<RequireQualifiedAccess>]
type ErrorArea =
    | Parse
    | Type
    | Symbol
    | InvalidArg
    | External
    | Bug
    | Auth

type TerrabuildException(msg, area, ?innerException: Exception) =
    inherit Exception(msg, innerException |> Option.toObj)
    member _.Area: ErrorArea = area

type private ParseErrorCollector =
    { Errors: ResizeArray<TerrabuildException>
      PosProvider: (unit -> (int * int) option) option }

let private parseErrorCollector = AsyncLocal<ParseErrorCollector option>()

let beginParseErrorCollection (posProvider: unit -> (int * int) option) =
    parseErrorCollector.Value <- Some { Errors = ResizeArray(); PosProvider = Some posProvider }

let endParseErrorCollection () =
    let errors =
        match parseErrorCollector.Value with
        | Some collector -> collector.Errors |> Seq.toList
        | None -> []

    parseErrorCollector.Value <- None
    errors

let private formatParseError (msg: string) (pos: (int * int) option) =
    if msg.StartsWith("Parse error at", StringComparison.Ordinal) then
        msg
    else
        match pos with
        | Some (line, col) -> sprintf "Parse error at (%d,%d): %s" line col msg
        | None -> msg

let private tryCollectParseError (msg: string) (inner: Exception option) (pos: (int * int) option) =
    match parseErrorCollector.Value with
    | Some collector ->
        let pos =
            match pos with
            | Some _ -> pos
            | None ->
                collector.PosProvider
                |> Option.bind (fun provider -> provider())

        let fullMsg = formatParseError msg pos
        let ex =
            match inner with
            | Some inner -> TerrabuildException(fullMsg, ErrorArea.Parse, inner)
            | None -> TerrabuildException(fullMsg, ErrorArea.Parse)

        collector.Errors.Add(ex)
        true
    | None -> false


let raiseInvalidArg(msg) =
    TerrabuildException(msg, ErrorArea.InvalidArg) |> raise

let forwardInvalidArg(msg, innerException) =
    TerrabuildException(msg, ErrorArea.InvalidArg, innerException) |> raise

let raiseParseError(msg) =
    if not (tryCollectParseError msg None None) then
        TerrabuildException(msg, ErrorArea.Parse) |> raise
    Unchecked.defaultof<'T>

let forwardParseError(msg, innerException) =
    if not (tryCollectParseError msg (Some innerException) None) then
        TerrabuildException(msg, ErrorArea.Parse, innerException) |> raise
    Unchecked.defaultof<'T>

let reportParseError(msg) =
    if not (tryCollectParseError msg None None) then
        TerrabuildException(msg, ErrorArea.Parse) |> raise

let reportParseErrorAt(line, col, msg) =
    if not (tryCollectParseError msg None (Some (line, col))) then
        let fullMsg = formatParseError msg (Some (line, col))
        TerrabuildException(fullMsg, ErrorArea.Parse) |> raise

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


let rec dumpKnownException (ex: Exception | null) =
    seq {
        match ex with
        | :? TerrabuildException as ex ->
            yield $"{ex.Message}"
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
