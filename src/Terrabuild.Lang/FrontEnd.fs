module Terrabuild.Lang.FrontEnd

open Errors
open Terrabuild.Expression

let private parseInternal (sourceName: string option) (stripLocations: bool) txt =
    try
        let file = Parser.parse sourceName txt
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
    | :? ParseFailure as exn ->
        let err = sprintf "Parse error at (%d,%d): %s" (exn.Position.Line + 1) (exn.Position.Column + 1) exn.Message
        forwardParseError(err, exn)
    | exn ->
        let err = sprintf "Unexpected token at parse entry: %s" exn.Message
        forwardParseError(err, exn)

let parse txt =
    parseInternal None true txt

let parseWithSource (sourceName: string) txt =
    parseInternal (Some sourceName) false txt
