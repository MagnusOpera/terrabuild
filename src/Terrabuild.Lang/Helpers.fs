module private Terrabuild.Lang.Helpers
open Terrabuild.Expressions
open Errors

let parseFunction expr = function
    | "trim" -> Expr.Function (Function.Trim, expr)
    | "upper" -> Expr.Function (Function.Upper, expr)
    | "lower" -> Expr.Function (Function.Lower, expr)
    | "replace" -> Expr.Function (Function.Replace, expr)
    | "count" -> Expr.Function (Function.Count, expr)
    | s ->
        Errors.reportParseError $"unknown function '{s}'"
        Expr.Nothing

let parseExpressionLiteral = function
    | "true" -> Expr.Bool true
    | "false" -> Expr.Bool false
    | "nothing" -> Expr.Nothing
    | s ->
        Errors.reportParseError $"unknown literal '{s}'"
        Expr.Nothing

let (|RegularIdentifier|ExtensionIdentifier|TargetIdentifier|) (value: string) =
    match value[0] with
    | '@' -> ExtensionIdentifier
    | '^' -> TargetIdentifier
    | _ -> RegularIdentifier

let parseResourceName s =
    match s with
    | ExtensionIdentifier | RegularIdentifier -> s
    | _ ->
        Errors.reportParseError $"invalid resource name '{s}'"
        s

let parseResourceIdentifier s =
    match s with
    | ExtensionIdentifier | RegularIdentifier -> s
    | _ ->
        Errors.reportParseError $"invalid resource identifier '{s}'"
        s

let parseAttributeName s =
    match s with
    | RegularIdentifier -> s
    | s ->
        Errors.reportParseError $"invalid attribute name '{s}'"
        s

let parseScopeIdentifier s =
    match s with
    | RegularIdentifier -> s
    | s ->
        Errors.reportParseError $"invalid scope identifier '{s}'"
        s

let parseIdentifier s =
    match s with
    | TargetIdentifier | RegularIdentifier -> s
    | _ ->
        Errors.reportParseError $"invalid resource identifier '{s}'"
        s
