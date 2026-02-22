namespace Terrabuild.Expression
open System

[<RequireQualifiedAccessAttribute>]
type Function =
    | Plus
    | Minus
    | Mult
    | Div
    | Trim
    | Upper
    | Lower
    | Replace
    | Count
    | Format
    | ToString
    | RegexMatch
    | Item
    | Coalesce
    | Ternary
    | Equal
    | NotEqual
    | Not
    | And
    | Or

[<RequireQualifiedAccessAttribute>]
type Expr =
    | Nothing
    | Bool of value:bool
    | String of value:string
    | Number of value:int
    | Enum of value:string
    | Map of Map<string, Expr>
    | List of Expr list
    | Variable of name:string
    | Function of Function * Expr list
with
    static member EmptyList = List []
    static member EmptyMap = Map Map.empty
    static member False = Bool false
    static member True = Bool true

[<RequireQualifiedAccess>]
type Value =
    | Nothing
    | Bool of bool
    | String of string
    | Number of int
    | Enum of string
    | Map of Map<string, Value>
    | List of Value list
    | Object of obj
with
    static member EmptyList = List []
    static member EmptyMap = Map Map.empty
    static member False = Bool false
    static member True = Bool true
