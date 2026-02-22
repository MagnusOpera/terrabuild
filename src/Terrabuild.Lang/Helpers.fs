module private Terrabuild.Lang.Helpers
open Terrabuild.Expression
open Errors

let parseFunction expr = function
    | "trim" -> Expr.Function (Function.Trim, expr)
    | "upper" -> Expr.Function (Function.Upper, expr)
    | "lower" -> Expr.Function (Function.Lower, expr)
    | "replace" -> Expr.Function (Function.Replace, expr)
    | "count" -> Expr.Function (Function.Count, expr)
    | s -> raiseParseError $"unknown function '{s}'"

let parseExpressionLiteral = function
    | "true" -> Expr.Bool true
    | "false" -> Expr.Bool false
    | "nothing" -> Expr.Nothing
    | s -> raiseParseError $"unknown literal '{s}'"

let (|RegularIdentifier|ExtensionIdentifier|TargetIdentifier|) (value: string) =
    match value[0] with
    | '@' -> ExtensionIdentifier
    | '^' -> TargetIdentifier
    | _ -> RegularIdentifier

let parseResourceName s =
    match s with
    | ExtensionIdentifier | RegularIdentifier -> s
    | _ -> raiseParseError $"invalid resource name '{s}'"

let parseResourceIdentifier s =
    match s with
    | ExtensionIdentifier | RegularIdentifier -> s
    | _ -> raiseParseError $"invalid resource identifier '{s}'"

let parseAttributeName s =
    match s with
    | RegularIdentifier -> s
    | s -> raiseParseError $"invalid attribute name '{s}'"

let parseScopeIdentifier s =
    match s with
    | RegularIdentifier -> s
    | s -> raiseParseError $"invalid scope identifier '{s}'"

let parseIdentifier s =
    match s with
    | TargetIdentifier | RegularIdentifier -> s
    | _ -> raiseParseError $"invalid resource identifier '{s}'"
