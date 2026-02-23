module Terrabuild.Lang.FrontEnd

open FSharp.Text.Lexing
open Errors
open System.Text
open System.Collections.Generic
open System
open Terrabuild.Expression

let private setSourceName (lexbuf: LexBuffer<char>) (sourceName: string option) =
    match sourceName with
    | Some sourceName when String.IsNullOrWhiteSpace(sourceName) |> not ->
        let startPos = { lexbuf.StartPos with pos_fname = sourceName }
        let endPos = { lexbuf.EndPos with pos_fname = sourceName }
        lexbuf.StartPos <- startPos
        lexbuf.EndPos <- endPos
    | _ -> ()

let private parseInternal (sourceName: string option) (stripLocations: bool) txt =
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
    setSourceName lexbuf sourceName
    try
        let file = Parser.File switchableLexer lexbuf
        if stripLocations then
            let stripAttribute (attribute: AST.Attribute) =
                { attribute with Value = Expr.StripLocations attribute.Value }

            let rec stripBlock (block: AST.Block) =
                { block with
                    Attributes = block.Attributes |> List.map stripAttribute
                    Blocks = block.Blocks |> List.map stripBlock }

            { file with Blocks = file.Blocks |> List.map stripBlock }
        else
            file
    with
    | :? TerrabuildException as exn ->
        let err = sprintf "Parse error at (%d,%d): %s"
                        (lexbuf.StartPos.Line + 1) (lexbuf.StartPos.Column + 1)
                        exn.Message
        forwardParseError(err, exn)
    | exn ->
        let err = sprintf "Unexpected token '%s' at (%d,%d): %s"
                        (LexBuffer<_>.LexemeString lexbuf |> string)
                        (lexbuf.StartPos.Line + 1) (lexbuf.StartPos.Column + 1)
                        exn.Message
        forwardParseError(err, exn)

let parse txt =
    parseInternal None true txt

let parseWithSource (sourceName: string) txt =
    parseInternal (Some sourceName) false txt
