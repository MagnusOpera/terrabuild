module Terrabuild.Lang.FrontEnd

open FSharp.Text.Lexing
open Errors
open System.Text
open System.Collections.Generic


let parse txt =
    let lexerMode = Stack([Lexer.LexerMode.Default])

    let switchableLexer (lexbuff: LexBuffer<char>) =
        let mode = lexerMode |> Lexer.peek
        let lexer = 
            match mode with
            | Lexer.LexerMode.Default -> Lexer.token lexerMode
            | Lexer.LexerMode.String -> Lexer.interpolatedString (StringBuilder()) lexerMode

        let token = lexer lexbuff
        // printfn $"### SwitchableLexer  mode: {mode}  token: {token}"
        token

    let lexbuf = LexBuffer<_>.FromString txt
    beginParseErrorCollection (fun () -> Some (lexbuf.StartPos.Line + 1, lexbuf.StartPos.Column + 1))
    let mutable result = Unchecked.defaultof<_>
    let mutable fatalException: exn option = None
    try
        result <- Parser.File switchableLexer lexbuf
    with
    | :? TerrabuildException as exn ->
        fatalException <- Some exn
        reportParseError exn.Message
    | exn ->
        fatalException <- Some exn
        let err = sprintf "Unexpected token '%s': %s"
                        (LexBuffer<_>.LexemeString lexbuf |> string)
                        exn.Message
        reportParseError err

    let errors = endParseErrorCollection()
    match errors with
    | [] ->
        match fatalException with
        | Some exn -> raise exn
        | None -> result
    | [single] ->
        TerrabuildException(single.Message, ErrorArea.Parse, single.InnerException) |> raise
    | errors ->
        let msg =
            "Parse errors:\n"
            + (errors |> List.map (fun e -> e.Message) |> String.concat "\n")
        TerrabuildException(msg, ErrorArea.Parse) |> raise
