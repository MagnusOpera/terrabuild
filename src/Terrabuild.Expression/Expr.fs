namespace Terrabuild.Expression
open System
open Errors

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
    | Located of location:SourceLocation * value:Expr
with
    static member EmptyList = List []
    static member EmptyMap = Map Map.empty
    static member False = Bool false
    static member True = Bool true
    static member WithLocation (location: SourceLocation) (value: Expr) = Located(location, value)
    static member TryGetLocation (expr: Expr) =
        let rec loop = function
            | Located (location, _) -> Some location
            | _ -> None
        loop expr
    static member StripLocations (expr: Expr) =
        let rec loop = function
            | Located (_, value) -> loop value
            | Nothing -> Nothing
            | Bool value -> Bool value
            | String value -> String value
            | Number value -> Number value
            | Enum value -> Enum value
            | Variable name -> Variable name
            | Map values -> values |> Map.map (fun _ item -> loop item) |> Map
            | List values -> values |> List.map loop |> List
            | Function (f, values) -> values |> List.map loop |> fun items -> Function (f, items)
        loop expr

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
