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
    try
        Parser.File switchableLexer lexbuf
    with
    | :? TerrabuildException as exn ->
        let err = sprintf "Parse error at (%d,%d): %s"
                        (lexbuf.StartPos.Line + 1) (lexbuf.StartPos.Column + 1)
                        exn.Message
        forwardParserError(err, exn)
    | exn ->
        let err = sprintf "Unexpected token '%s' at (%d,%d): %s"
                        (LexBuffer<_>.LexemeString lexbuf |> string) 
                        (lexbuf.StartPos.Line + 1) (lexbuf.StartPos.Column + 1)
                        exn.Message
        forwardParserError(err, exn)
