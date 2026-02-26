namespace Terrabuild.Lang

open System.Collections.Generic
open System
open System.Text
open Errors

[<RequireQualifiedAccess>]
type private LexerMode =
    | Default
    | String

type Lexer(text: string, sourceName: string option) =
    let mutable index = 0
    let mutable line = 0
    let mutable column = 0
    let modes = Stack<LexerMode>()
    let pending = Queue<Token>()

    do modes.Push LexerMode.Default

    let currentPos () =
        { Position.Offset = index
          Position.Line = line
          Position.Column = column
          Position.SourceName = sourceName }

    let length = text.Length

    let mkToken startPos endPos kind =
        { Token.Kind = kind
          Token.StartPos = startPos
          Token.EndPos = endPos }

    let has i = i < length
    let ch i = text[i]

    let isAlpha c =
        (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')

    let isDigit c =
        c >= '0' && c <= '9'

    let isIdentTail c =
        isAlpha c || isDigit c || c = '_'

    let tryConsume (value: string) =
        let ok =
            index + value.Length <= length
            && String.CompareOrdinal(text, index, value, 0, value.Length) = 0
        if ok then
            let startPos = currentPos()
            for _ in 1..value.Length do
                let c = ch index
                index <- index + 1
                column <- column + 1
            let endPos = currentPos()
            Some(startPos, endPos)
        else
            None

    let bumpOne () =
        if has index then
            let c = ch index
            index <- index + 1
            if c = '\r' && has index && ch index = '\n' then
                index <- index + 1
                line <- line + 1
                column <- 0
            elif c = '\n' then
                line <- line + 1
                column <- 0
            else
                column <- column + 1
            Some c
        else
            None

    let consumeSingle kind =
        let startPos = currentPos()
        bumpOne () |> ignore
        let endPos = currentPos()
        mkToken startPos endPos kind

    let consumeWhile predicate =
        let sb = StringBuilder()
        while has index && predicate (ch index) do
            sb.Append(ch index) |> ignore
            bumpOne() |> ignore
        sb.ToString()

    let consumeIdentifier () =
        let startPos = currentPos()
        let first = ch index
        bumpOne() |> ignore
        let tail = consumeWhile isIdentTail
        let value = string first + tail
        let endPos = currentPos()
        value, startPos, endPos

    let mkIdentifier (s: string) =
        s.Replace("`", "").Replace(" ", "").Replace(":", "").Replace("$", "").Replace("~", "")

    let rec skipWhitespaceAndComments () =
        if has index then
            match ch index with
            | ' '
            | '\t' ->
                bumpOne() |> ignore
                skipWhitespaceAndComments()
            | '\n'
            | '\r' ->
                bumpOne() |> ignore
                skipWhitespaceAndComments()
            | '#' ->
                while has index && ch index <> '\n' && ch index <> '\r' do
                    bumpOne() |> ignore
                skipWhitespaceAndComments()
            | _ -> ()

    let rec lexDefault () =
        skipWhitespaceAndComments()
        if not (has index) then
            let p = currentPos()
            mkToken p p TokenKind.Eof
        else
            match tryConsume ".[" with
            | Some (s, e) -> mkToken s e TokenKind.DotLSqBracket
            | None ->
                match tryConsume "??" with
                | Some (s, e) -> mkToken s e TokenKind.DoubleQuestion
                | None ->
                    match tryConsume "==" with
                    | Some (s, e) -> mkToken s e TokenKind.DoubleEqual
                    | None ->
                        match tryConsume "!=" with
                        | Some (s, e) -> mkToken s e TokenKind.NotEqual
                        | None ->
                            match tryConsume "&&" with
                            | Some (s, e) -> mkToken s e TokenKind.And
                            | None ->
                                match tryConsume "||" with
                                | Some (s, e) -> mkToken s e TokenKind.Or
                                | None ->
                                    match tryConsume "~=" with
                                    | Some (s, e) -> mkToken s e TokenKind.RegexMatch
                                    | None ->
                                        if ch index = '-' && index + 1 < length && isDigit (ch (index + 1)) then
                                            let startPos = currentPos()
                                            bumpOne() |> ignore
                                            let digits = consumeWhile isDigit
                                            let endPos = currentPos()
                                            let v = int ("-" + digits)
                                            mkToken startPos endPos (TokenKind.Number v)
                                        elif isDigit (ch index) then
                                            let startPos = currentPos()
                                            let digits = consumeWhile isDigit
                                            let endPos = currentPos()
                                            mkToken startPos endPos (TokenKind.Number (int digits))
                                        elif ch index = '"' then
                                            modes.Push LexerMode.String
                                            consumeSingle TokenKind.StringStart
                                        elif ch index = '{' then
                                            let startPos = currentPos()
                                            bumpOne() |> ignore
                                            modes.Push LexerMode.Default
                                            let endPos = currentPos()
                                            mkToken startPos endPos TokenKind.LBrace
                                        elif ch index = '}' then
                                            let startPos = currentPos()
                                            bumpOne() |> ignore
                                            if modes.Count > 0 then
                                                modes.Pop() |> ignore
                                            let endPos = currentPos()
                                            let kind =
                                                if modes.Count > 0 && modes.Peek() = LexerMode.String then
                                                    TokenKind.ExpressionEnd
                                                else
                                                    TokenKind.RBrace
                                            mkToken startPos endPos kind
                                        elif ch index = '~' && index + 1 < length && isAlpha (ch (index + 1)) then
                                            let startPos = currentPos()
                                            bumpOne() |> ignore
                                            let value, _, _ = consumeIdentifier()
                                            let endPos = currentPos()
                                            mkToken startPos endPos (TokenKind.Enum (mkIdentifier value))
                                        elif ch index = '@' || ch index = '^' || isAlpha (ch index) then
                                            let value, startPos, endPos = consumeIdentifier()
                                            if isAlpha value[0] && has index && ch index = ':' then
                                                bumpOne() |> ignore
                                                let endWithColon = currentPos()
                                                mkToken startPos endWithColon (TokenKind.Key (mkIdentifier value))
                                            else
                                                mkToken startPos endPos (TokenKind.Identifier (mkIdentifier value))
                                        else
                                            match ch index with
                                            | '.' -> consumeSingle TokenKind.Dot
                                            | ':' -> consumeSingle TokenKind.Colon
                                            | '[' -> consumeSingle TokenKind.LSqBracket
                                            | ']' -> consumeSingle TokenKind.RSqBracket
                                            | '(' -> consumeSingle TokenKind.LParen
                                            | ')' -> consumeSingle TokenKind.RParen
                                            | '=' -> consumeSingle TokenKind.Equal
                                            | ',' -> consumeSingle TokenKind.Comma
                                            | '-' -> consumeSingle TokenKind.Minus
                                            | '+' -> consumeSingle TokenKind.Plus
                                            | '*' -> consumeSingle TokenKind.Mult
                                            | '/' -> consumeSingle TokenKind.Div
                                            | '?' -> consumeSingle TokenKind.Question
                                            | '!' -> consumeSingle TokenKind.Bang
                                            | c -> raiseParseError $"unrecognized input: '{string c}'"

    let rec lexString () =
        let startPos = currentPos()
        let acc = StringBuilder()
        let rec loop () =
            if not (has index) then
                raiseParseError "newline encountered in string"
            elif ch index = '\n' || ch index = '\r' then
                raiseParseError "newline encountered in string"
            elif index + 1 < length && ch index = '"' && ch (index + 1) = '"' then
                bumpOne() |> ignore
                bumpOne() |> ignore
                acc.Append('"') |> ignore
                loop()
            elif index + 1 < length && ch index = '{' && ch (index + 1) = '{' then
                bumpOne() |> ignore
                bumpOne() |> ignore
                acc.Append('{') |> ignore
                loop()
            elif index + 1 < length && ch index = '}' && ch (index + 1) = '}' then
                bumpOne() |> ignore
                bumpOne() |> ignore
                acc.Append('}') |> ignore
                loop()
            elif ch index = '"' then
                bumpOne() |> ignore
                if modes.Count > 0 then
                    modes.Pop() |> ignore
                let endPos = currentPos()
                mkToken startPos endPos (TokenKind.StringEnd (acc.ToString()))
            elif index + 1 < length && ch index = '$' && ch (index + 1) = '{' then
                bumpOne() |> ignore
                bumpOne() |> ignore
                modes.Push LexerMode.Default
                let endPos = currentPos()
                mkToken startPos endPos (TokenKind.ExpressionStart (acc.ToString()))
            else
                acc.Append(ch index) |> ignore
                bumpOne() |> ignore
                loop()
        loop()

    member _.NextToken() =
        if pending.Count > 0 then
            pending.Dequeue()
        else
            let mode =
                if modes.Count = 0 then LexerMode.Default else modes.Peek()
            match mode with
            | LexerMode.Default -> lexDefault()
            | LexerMode.String -> lexString()
