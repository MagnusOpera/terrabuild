namespace Terrabuild.Lang

open System
open Terrabuild.Expression
open Terrabuild.Lang.AST
open Helpers
open Errors

type ParseFailure(message: string, position: Position, ?inner: exn) =
    inherit Exception(message, defaultArg inner null)
    member _.Position = position

module Parser =
    let private toSourceLocation (startPos: Position) (endPos: Position) =
        { Errors.SourceLocation.File = startPos.SourceName
          Errors.SourceLocation.StartLine = startPos.Line + 1
          Errors.SourceLocation.StartColumn = startPos.Column + 1
          Errors.SourceLocation.EndLine = endPos.Line + 1
          Errors.SourceLocation.EndColumn = endPos.Column + 1 }

    let private withLocation (startPos: Position) (endPos: Position) (expr: Expr) =
        Expr.WithLocation (toSourceLocation startPos endPos) expr

    type private State(lexer: Lexer) =
        let lookahead = Collections.Generic.List<Token>()

        member _.Peek(offset: int) =
            while lookahead.Count <= offset do
                lookahead.Add(lexer.NextToken())
            lookahead[offset]

        member this.Current = this.Peek(0)

        member this.Advance() =
            this.Peek(0) |> ignore
            let token = lookahead[0]
            lookahead.RemoveAt(0)
            token

        member this.Expect(kind: TokenKind) =
            let token = this.Current
            if token.Kind = kind then this.Advance()
            else raise (ParseFailure($"unexpected token '{token.Kind}'", token.StartPos))

    let private wrapWithStateError (state: State) (f: unit -> 'a) =
        try
            f()
        with
        | :? ParseFailure ->
            reraise()
        | :? TerrabuildException as ex ->
            raise (ParseFailure(ex.Message, state.Current.StartPos, ex))

    let private wrapWithPositionError (position: Position) (f: unit -> 'a) =
        try
            f()
        with
        | :? ParseFailure ->
            reraise()
        | :? TerrabuildException as ex ->
            raise (ParseFailure(ex.Message, position, ex))

    let private tokenToString = function
        | TokenKind.StringStart -> "string-start"
        | TokenKind.StringEnd _ -> "string-end"
        | TokenKind.ExpressionStart _ -> "expr-start"
        | TokenKind.ExpressionEnd -> "expr-end"
        | TokenKind.Identifier s -> s
        | TokenKind.Key s -> s + ":"
        | TokenKind.Enum s -> "~" + s
        | TokenKind.Number n -> string n
        | TokenKind.LBrace -> "{"
        | TokenKind.RBrace -> "}"
        | TokenKind.Dot -> "."
        | TokenKind.LSqBracket -> "["
        | TokenKind.RSqBracket -> "]"
        | TokenKind.DotLSqBracket -> ".["
        | TokenKind.LParen -> "("
        | TokenKind.RParen -> ")"
        | TokenKind.Equal -> "="
        | TokenKind.DoubleEqual -> "=="
        | TokenKind.NotEqual -> "!="
        | TokenKind.Comma -> ","
        | TokenKind.Minus -> "-"
        | TokenKind.Plus -> "+"
        | TokenKind.Mult -> "*"
        | TokenKind.Div -> "/"
        | TokenKind.DoubleQuestion -> "??"
        | TokenKind.Question -> "?"
        | TokenKind.Colon -> ":"
        | TokenKind.Bang -> "!"
        | TokenKind.And -> "&&"
        | TokenKind.Or -> "||"
        | TokenKind.RegexMatch -> "~="
        | TokenKind.Eof -> "eof"

    let parse (sourceName: string option) (txt: string) : File =
        let state = State(Lexer(txt, sourceName))

        let unexpected token =
            raise (ParseFailure($"unexpected token '{tokenToString token.Kind}'", token.StartPos))

        let rec parseFile () =
            let blocks = parseBlocksUntil TokenKind.Eof
            state.Expect TokenKind.Eof |> ignore
            File.Build blocks

        and parseBlocksUntil endToken =
            let blocks = ResizeArray<Block>()
            while state.Current.Kind <> endToken && state.Current.Kind <> TokenKind.Eof do
                blocks.Add(parseBlock())
            blocks |> Seq.toList

        and parseBlock () =
            let resource = parseResourceNameToken()
            let blockId =
                match state.Current.Kind with
                | TokenKind.Identifier _ -> Some(parseResourceIdentifierToken())
                | _ -> None

            state.Expect TokenKind.LBrace |> ignore
            let attributes, blocks = parseBlockContent()
            state.Expect TokenKind.RBrace |> ignore
            Block.Build resource blockId (attributes, blocks)

        and parseBlockContent () =
            let attributes = ResizeArray<Attribute>()
            let blocks = ResizeArray<Block>()
            let mutable doneLoop = false
            while not doneLoop do
                match state.Current.Kind with
                | TokenKind.RBrace
                | TokenKind.Eof -> doneLoop <- true
                | TokenKind.Identifier _ when state.Peek(1).Kind = TokenKind.Equal && blocks.Count = 0 ->
                    let attribute = parseAttribute()
                    let next = wrapWithStateError state (fun () -> Attribute.Append (attributes |> Seq.toList) attribute)
                    attributes.Clear()
                    next |> List.iter attributes.Add
                | TokenKind.Identifier _ when state.Peek(1).Kind = TokenKind.LBrace || (match state.Peek(1).Kind with | TokenKind.Identifier _ -> true | _ -> false) ->
                    blocks.Add(parseBlock())
                | _ ->
                    unexpected state.Current
            (attributes |> Seq.toList), (blocks |> Seq.toList)

        and parseAttribute () =
            let name = parseAttributeNameToken()
            state.Expect TokenKind.Equal |> ignore
            let value, _, _ = parseExpr()
            Attribute.Build name value

        and parseExpr () =
            parseTernary()

        and parseTernary () =
            let leftExpr, leftStart, leftEnd = parseCoalesce()
            if state.Current.Kind = TokenKind.Question then
                state.Advance() |> ignore
                let trueExpr, _, _ = parseExpr()
                state.Expect TokenKind.Colon |> ignore
                let falseExpr, _, falseEnd = parseExpr()
                withLocation leftStart falseEnd (Expr.Function (Function.Ternary, [ leftExpr; trueExpr; falseExpr ])), leftStart, falseEnd
            else
                leftExpr, leftStart, leftEnd

        and parseCoalesce () =
            let leftExpr, leftStart, leftEnd = parseOr()
            if state.Current.Kind = TokenKind.DoubleQuestion then
                state.Advance() |> ignore
                let rightExpr, _, rightEnd = parseCoalesce()
                withLocation leftStart rightEnd (Expr.Function (Function.Coalesce, [ leftExpr; rightExpr ])), leftStart, rightEnd
            else
                leftExpr, leftStart, leftEnd

        and parseOr () =
            parseLeftAssoc parseAnd [ TokenKind.Or, Function.Or ]

        and parseAnd () =
            parseLeftAssoc parseEquality [ TokenKind.And, Function.And ]

        and parseEquality () =
            parseLeftAssoc parseRegex [ TokenKind.DoubleEqual, Function.Equal; TokenKind.NotEqual, Function.NotEqual ]

        and parseRegex () =
            let mutable leftExpr, leftStart, leftEnd = parseAdditive()
            let mutable loop = true
            while loop do
                if state.Current.Kind = TokenKind.RegexMatch then
                    state.Advance() |> ignore
                    let rightExpr, _, rightEnd = parseAdditive()
                    leftExpr <- withLocation leftStart rightEnd (Expr.Function(Function.RegexMatch, [ rightExpr; leftExpr ]))
                    leftEnd <- rightEnd
                else
                    loop <- false
            leftExpr, leftStart, leftEnd

        and parseAdditive () =
            parseLeftAssoc parseMultiplicative [ TokenKind.Plus, Function.Plus; TokenKind.Minus, Function.Minus ]

        and parseMultiplicative () =
            parseLeftAssoc parseUnary [ TokenKind.Mult, Function.Mult; TokenKind.Div, Function.Div ]

        and parseLeftAssoc parseNext (ops: (TokenKind * Function) list) =
            let mutable leftExpr, leftStart, leftEnd = parseNext()
            let mutable loop = true
            while loop do
                match ops |> List.tryFind (fun (kind, _) -> state.Current.Kind = kind) with
                | Some (_, fn) ->
                    state.Advance() |> ignore
                    let rightExpr, _, rightEnd = parseNext()
                    leftExpr <- withLocation leftStart rightEnd (Expr.Function(fn, [ leftExpr; rightExpr ]))
                    leftEnd <- rightEnd
                | None -> loop <- false
            leftExpr, leftStart, leftEnd

        and parseUnary () =
            match state.Current.Kind with
            | TokenKind.Bang ->
                let bang = state.Advance()
                let sourceExpr, _, sourceEnd = parseExprSource()
                withLocation bang.StartPos sourceEnd (Expr.Function(Function.Not, [ sourceExpr ])), bang.StartPos, sourceEnd
            | _ ->
                let sourceExpr, sourceStart, sourceEnd = parseExprSource()
                withLocation sourceStart sourceEnd sourceExpr, sourceStart, sourceEnd

        and parseExprSource () =
            match state.Current.Kind with
            | TokenKind.LSqBracket -> parseExprList()
            | TokenKind.LBrace -> parseExprMap()
            | TokenKind.Number n ->
                let t = state.Advance()
                Expr.Number n, t.StartPos, t.EndPos
            | TokenKind.Enum s ->
                let t = state.Advance()
                Expr.Enum s, t.StartPos, t.EndPos
            | TokenKind.StringStart ->
                parseInterpolatedString()
            | TokenKind.Identifier _ ->
                if state.Peek(1).Kind = TokenKind.LParen then
                    parseFunction()
                elif state.Peek(1).Kind = TokenKind.Dot && (match state.Peek(2).Kind with | TokenKind.Identifier _ -> true | _ -> false) then
                    parseVariable()
                else
                    parseExprLiteralToken()
            | _ ->
                unexpected state.Current

        and parseFunction () =
            let nameToken = state.Advance()
            let name =
                match nameToken.Kind with
                | TokenKind.Identifier s -> s
                | _ -> unexpected nameToken
            state.Expect TokenKind.LParen |> ignore
            let args = parseExprTupleContent()
            let close = state.Expect TokenKind.RParen
            let expr =
                wrapWithPositionError close.StartPos (fun () -> Helpers.parseFunction args name)
            expr, nameToken.StartPos, close.EndPos

        and parseVariable () =
            let scopeToken = state.Advance()
            let scopeName =
                match scopeToken.Kind with
                | TokenKind.Identifier s -> s
                | _ -> unexpected scopeToken
            state.Expect TokenKind.Dot |> ignore
            let idToken = state.Advance()
            let identifier =
                match idToken.Kind with
                | TokenKind.Identifier s -> s
                | _ -> unexpected idToken

            let baseVar =
                wrapWithPositionError idToken.StartPos (fun () ->
                    let scope = parseScopeIdentifier scopeName
                    let id = parseIdentifier identifier
                    Expr.Variable $"{scope}.{id}")

            let mutable expr = baseVar
            let mutable startPos = scopeToken.StartPos
            let mutable endPos = idToken.EndPos
            let mutable loop = true
            while loop do
                match state.Current.Kind with
                | TokenKind.Dot ->
                    let dot = state.Advance()
                    let itemToken = state.Advance()
                    let indexExpr =
                        match itemToken.Kind with
                        | TokenKind.Number n -> Expr.Number n
                        | TokenKind.Identifier s -> Expr.String s
                        | _ -> unexpected itemToken
                    expr <- Expr.Function(Function.Item, [ expr; indexExpr ])
                    endPos <- itemToken.EndPos
                | TokenKind.DotLSqBracket ->
                    state.Advance() |> ignore
                    let valueExpr, _, valueEnd = parseExpr()
                    let close = state.Expect TokenKind.RSqBracket
                    expr <- Expr.Function(Function.Item, [ expr; valueExpr ])
                    endPos <- close.EndPos
                | _ ->
                    loop <- false

            expr, startPos, endPos

        and parseExprTupleContent () =
            let values = ResizeArray<Expr>()
            if state.Current.Kind <> TokenKind.RParen then
                let first, _, _ = parseExpr()
                values.Add first
                while state.Current.Kind = TokenKind.Comma do
                    state.Advance() |> ignore
                    let item, _, _ = parseExpr()
                    values.Add item
            values |> Seq.toList

        and parseExprList () =
            let openT = state.Expect TokenKind.LSqBracket
            let items = ResizeArray<Expr>()
            while state.Current.Kind <> TokenKind.RSqBracket do
                let expr, _, _ = parseExpr()
                items.Add expr
                if state.Current.Kind = TokenKind.Comma then
                    state.Advance() |> ignore
            let close = state.Expect TokenKind.RSqBracket
            Expr.List (items |> Seq.toList), openT.StartPos, close.EndPos

        and parseExprMap () =
            let openT = state.Expect TokenKind.LBrace
            let mutable map = Map.empty
            while state.Current.Kind <> TokenKind.RBrace do
                if state.Current.Kind = TokenKind.Comma then
                    state.Advance() |> ignore
                let keyToken = state.Advance()
                let key =
                    match keyToken.Kind with
                    | TokenKind.Key s -> s
                    | _ -> unexpected keyToken
                let valueExpr, _, _ = parseExpr()
                map <- map.Add(key, valueExpr)
            let close = state.Expect TokenKind.RBrace
            Expr.Map map, openT.StartPos, close.EndPos

        and parseInterpolatedExpression () =
            let token = state.Advance()
            let prefix =
                match token.Kind with
                | TokenKind.ExpressionStart s -> s
                | _ -> unexpected token
            let expr, _, _ = parseExpr()
            state.Expect TokenKind.ExpressionEnd |> ignore
            prefix, expr

        and parseInterpolatedString () =
            let openToken = state.Expect TokenKind.StringStart
            let rec parseSegments acc =
                match state.Current.Kind with
                | TokenKind.StringEnd s ->
                    let closeToken = state.Advance()
                    let value =
                        if acc = None then
                            Expr.String s
                        else
                            let e = acc.Value
                            if String.IsNullOrEmpty s then Expr.Function(Function.ToString, [ e ])
                            else Expr.Function(Function.Format, [ Expr.String "{0}{1}"; e; Expr.String s ])
                    value, openToken.StartPos, closeToken.EndPos
                | TokenKind.ExpressionStart _ ->
                    let prefix, expr = parseInterpolatedExpression()
                    let next =
                        match acc with
                        | None ->
                            if String.IsNullOrEmpty prefix then expr
                            else Expr.Function(Function.Format, [ Expr.String "{0}{1}"; Expr.String prefix; expr ])
                        | Some left ->
                            if String.IsNullOrEmpty prefix then Expr.Function(Function.Format, [ Expr.String "{0}{1}"; left; expr ])
                            else Expr.Function(Function.Format, [ Expr.String "{0}{1}{2}"; left; Expr.String prefix; expr ])
                    parseSegments (Some next)
                | _ ->
                    unexpected state.Current
            parseSegments None

        and parseResourceNameToken () =
            let t = state.Current
            match t.Kind with
            | TokenKind.Identifier s ->
                let result = wrapWithStateError state (fun () -> parseResourceName s)
                state.Advance() |> ignore
                result
            | _ -> unexpected t

        and parseResourceIdentifierToken () =
            let t = state.Current
            match t.Kind with
            | TokenKind.Identifier s ->
                let result = wrapWithStateError state (fun () -> parseResourceIdentifier s)
                state.Advance() |> ignore
                result
            | _ -> unexpected t

        and parseAttributeNameToken () =
            let t = state.Advance()
            match t.Kind with
            | TokenKind.Identifier s ->
                wrapWithStateError state (fun () -> parseAttributeName s)
            | _ -> unexpected t

        and parseExprLiteralToken () =
            let t = state.Advance()
            match t.Kind with
            | TokenKind.Identifier s ->
                let expr = wrapWithStateError state (fun () -> parseExpressionLiteral s)
                expr, t.StartPos, t.EndPos
            | _ -> unexpected t

        parseFile()
