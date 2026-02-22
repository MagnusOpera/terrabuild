module Terrabuild.Expression.Eval
open Terrabuild.Expression
open Errors
open Collections
open System.Text.RegularExpressions

type EvaluationContext =
    { WorkspaceDir: string option
      ProjectDir: string option
      Data: Map<string, Value> }
with
    static member Empty =
        { WorkspaceDir = None
          ProjectDir = None
          Data = Map.empty }

let isTruthy = function
    | Value.Bool false
    | Value.Nothing -> false
    | _ -> true


let valueToString v =
    match v with
    | Value.Nothing -> ""
    | Value.Bool b -> if b then "true" else "false"
    | Value.Number n -> $"{n}"
    | Value.String s -> s
    | _ -> raiseTypeError $"Unsupported type for format {v}" (v.ToString())

let rec eval (context: EvaluationContext) (expr: Expr) =
    let rec eval (expr: Expr) =
        match expr with
        | Expr.Nothing -> Value.Nothing
        | Expr.Bool bool -> Value.Bool bool
        | Expr.String str -> Value.String str
        | Expr.Number num -> Value.Number num
        | Expr.Enum enum -> Value.Enum enum
        | Expr.Variable var ->
            match context.Data |> Map.tryFind var with
            | Some value -> value
            | None -> raiseSymbolError $"Variable '{var}' is not defined"
        | Expr.Map map ->
            let values = map |> Map.fold (fun map k v ->
                let mv = eval v
                map |> Map.add k mv) Map.empty
            Value.Map values
        | Expr.List list ->
            let values = list |> List.fold (fun list v ->
                let mv = eval v
                list @ [mv]) []
            Value.List values
        | Expr.Function (f, exprs) ->
            let values = exprs |> List.fold (fun exprs expr ->
                let e = eval expr
                exprs @ [e]) []
            let res =
                match f, values with
                | Function.Plus, [Value.String left; Value.String right] -> Value.String (left + right)
                | Function.Upper, [Value.String str] -> Value.String (str.ToUpperInvariant())
                | Function.Trim, [Value.String str] -> Value.String (str.Trim())
                | Function.Lower, [Value.String str] -> Value.String (str.ToLowerInvariant())
                | Function.Replace, [Value.String str; Value.String value; Value.String newValue] -> Value.String (str.Replace(value, newValue))

                | Function.Plus, [Value.Number left; Value.Number right] -> Value.Number (left + right)
                | Function.Minus, [Value.Number left; Value.Number right] -> Value.Number (left - right)

                | Function.Mult, [Value.Number left; Value.Number right] -> Value.Number (left * right)
                | Function.Div, [Value.Number left; Value.Number right] -> Value.Number (left / right)

                | Function.Plus, [Value.Map left; Value.Map right] -> Value.Map (left |> Map.addMap right)
                | Function.Count, [Value.Map map] -> Value.Number map.Count

                | Function.Plus, [Value.List left; Value.List right] -> Value.List (left @ right)
                | Function.Count, [Value.List list] -> Value.Number list.Length

                | Function.Not, [value] -> isTruthy value |> not |> Value.Bool 

                | Function.And, [left; right] -> Value.Bool (isTruthy left && isTruthy right)
                | Function.Or, [left; right] -> Value.Bool (isTruthy left || isTruthy right)

                | Function.ToString, [value] -> valueToString value |> Value.String

                | Function.Format, Value.String template :: values ->
                    let values = values |> List.map valueToString

                    let rec replaceAll template =
                        match template with
                        | String.Regex "{([^}]+)}" [index] ->
                            let value =
                                match System.Int32.TryParse index with
                                | (true, index) -> 
                                    if 0 <= index && index < values.Length then values[index]
                                    else raiseInvalidArg $"Format index is out of range"
                                | _ -> raiseTypeError $"Format index is not a number" index
                            template
                            |> String.replace $"{{{index}}}" value
                            |> replaceAll
                        | _ -> template

                    replaceAll template |> Value.String

                | Function.RegexMatch, [Value.String pattern; value] ->
                    match value with
                    | Value.String str -> Regex(pattern).IsMatch(str) |> Value.Bool
                    | _ -> Value.Bool false

                | Function.Item, [Value.Map map; Value.String key] ->
                    match map |> Map.tryFind key with
                    | Some value -> value
                    | _ -> raiseSymbolError $"Unknown key {key}"

                | Function.Item, [Value.List list; Value.Number index] ->
                    match list |> List.tryItem index with
                    | Some value -> value
                    | _ -> raiseInvalidArg $"Out of range index {index}"

                | Function.Coalesce, [leftValue; rightValue] ->
                    let condition = isTruthy leftValue
                    if condition then leftValue else rightValue

                | Function.Ternary, [condition; trueValue; falseValue] ->
                    let condition = isTruthy condition
                    if condition then trueValue else falseValue

                | Function.Equal, [left; right] ->
                    Value.Bool (left = right)

                | Function.NotEqual, [left; right] ->
                    Value.Bool (left <> right)

                | f, prms -> 
                    let getParamType (value: Value ) =
                        match value with
                        | Value.Bool _ -> "bool"
                        | Value.List _ -> "list"
                        | Value.Map _ -> "map"
                        | Value.Nothing -> "nothing"
                        | Value.Number _ -> "number"
                        | Value.Enum _ -> "enum"
                        | Value.Object _ -> "object"
                        | Value.String _ -> "string"

                    let prms = prms |> List.map (getParamType) |> String.join "*"
                    raiseInvalidArg $"Invalid arguments for function {f} with parameters ({prms})"
            res

    eval expr

let asStringOption = function
    | Value.String s -> s |> Some
    | Value.Nothing -> None
    | x -> raiseTypeError $"Failed to convert '{x}' to optional string"

let asString = function
    | Value.String s -> s
    | x -> raiseTypeError $"Failed to convert '{x}' to string"

let asEnum = function
    | Value.Enum s -> Ok s
    | x -> Error $"Failed to convert '{x}' to enum"

let asBoolOption = function
    | Value.Bool b -> b |> Some
    | Value.Nothing -> None
    | x -> raiseTypeError $"Failed to convert '{x}' to optional boolean"

let asStringSetOption = function
    | Value.List list ->
        list
        |> List.map (fun value ->
            match value with
            | Value.String s -> s
            | x -> raiseTypeError $"Failed to convert '{x}' to string list")
        |> Set.ofList
        |> Some
    | x -> raiseTypeError $"Failed to convert '{x}' to optional list"

let mapAdd newMap oldMap =
    match oldMap, newMap with
    | Value.Map oldMap, Value.Map newMap ->
        oldMap |> Map.addMap newMap |> Value.Map
    | x -> raiseTypeError $"Failed to convert '{x}' to map: invalid types"

let asMap = function
    | Value.Map map -> map
    | x -> raiseTypeError $"Failed to convert '{x}' to map"
