namespace Terrabuild.Lang

[<StructuralEquality; StructuralComparison>]
type Position =
    { Offset: int
      Line: int
      Column: int
      SourceName: string option }

[<RequireQualifiedAccess>]
type TokenKind =
    | StringStart
    | StringEnd of string
    | ExpressionStart of string
    | ExpressionEnd
    | Identifier of string
    | Key of string
    | Enum of string
    | Number of int
    | LBrace
    | RBrace
    | Dot
    | LSqBracket
    | RSqBracket
    | DotLSqBracket
    | LParen
    | RParen
    | Equal
    | DoubleEqual
    | NotEqual
    | Comma
    | Minus
    | Plus
    | Mult
    | Div
    | DoubleQuestion
    | Question
    | Colon
    | Bang
    | And
    | Or
    | RegexMatch
    | Eof

type Token =
    { Kind: TokenKind
      StartPos: Position
      EndPos: Position }
