module Terrabuild.Expression.Dependencies
open System.Collections.Generic
open System.Collections.Concurrent
open Terrabuild.Expression
open FSharp.Reflection


let rec find (expr: Expr) =
    let rec eval (varUsed: Set<string>) (expr: Expr) =
        match expr with
        | Expr.Located (_, value) -> eval varUsed value
        | Expr.Variable var -> varUsed |> Set.add var
        | Expr.Map map -> map |> Map.fold (fun varUsed _ v -> varUsed + eval varUsed v) varUsed
        | Expr.List list -> list |> List.fold (fun varUsed v -> varUsed + eval varUsed v) varUsed
        | Expr.Function (_, exprs) -> exprs |> List.fold (fun varUsed expr -> varUsed + eval varUsed expr) varUsed
        | _ -> varUsed

    eval Set.empty expr


let rec findArrayOfDependencies (expr: Expr) =

    let rec eval (varUsed: Set<string>) (expr: Expr) =
        match expr with
        | Expr.Located (_, value) -> eval varUsed value
        | Expr.Variable var -> varUsed |> Set.add var
        | _ -> Errors.raiseInvalidArg "Array of dependencies expected"

    match expr with
    | Expr.Located (_, value) -> findArrayOfDependencies value
    | Expr.List list -> list |> List.fold (fun varUsed v -> varUsed + eval varUsed v) Set.empty
    | _ -> Errors.raiseInvalidArg "Array of dependencies expected"

let rec findProjectReferences (expr: Expr) =
    let rec tryFindProjectReference expr =
        match Expr.StripLocations expr with
        | Expr.Function (Function.Item, [Expr.Variable "project"; Expr.String projectName]) -> Some projectName
        | Expr.Function (Function.Item, [parent; _]) -> tryFindProjectReference parent
        | _ -> None

    let rec eval (projects: Set<string>) (expr: Expr) =
        let projects =
            match expr with
            | Expr.Located (_, value) -> eval projects value
            | Expr.Map map -> map |> Map.fold (fun acc _ v -> eval acc v) projects
            | Expr.List list -> list |> List.fold eval projects
            | Expr.Function (_, exprs) -> exprs |> List.fold eval projects
            | _ -> projects

        match tryFindProjectReference expr with
        | Some projectName -> projects |> Set.add projectName
        | None -> projects

    eval Set.empty expr


let private fsmapTy = typedefof<Map<string, _>>
let private fsoptionTy = typedefof<option<_>>
let private fskvpTy = typedefof<KeyValuePair<string, _>>
// Cache type metadata, never configuration values or dependency results.
let private readers = ConcurrentDictionary<System.Type, obj -> (obj | null) array>()

let private buildReader (ty: System.Type) : obj -> (obj | null) array =
    if ty.IsGenericType then
        let definition = ty.GetGenericTypeDefinition()
        let property =
            if definition = fsmapTy then ty.GetProperty("Values")
            elif definition = fsoptionTy || definition = fskvpTy then ty.GetProperty("Value")
            else null
        match property with
        | NonNull property -> fun value -> [| property.GetValue(value, null) |]
        | _ -> fun _ -> Array.empty
    elif FSharpType.IsRecord(ty, false) then
        let reader = FSharpValue.PreComputeRecordReader(ty, false)
        fun value -> reader value |> Array.map (fun field -> (field: obj | null))
    else fun _ -> Array.empty

let private readerFactory = System.Func<System.Type, obj -> (obj | null) array>(buildReader)

let private visitExpressions visit (value: obj) =
    let pending = Stack<obj | null>()
    pending.Push(value)
    while pending.Count > 0 do
        match pending.Pop() with
        | :? string -> ()
        | :? System.Collections.IEnumerable as enumerable ->
            for item in enumerable do pending.Push(item)
        | :? Expr as expr -> visit expr
        | NonNull value ->
            let reader = readers.GetOrAdd(value.GetType(), readerFactory)
            for field in reader value do pending.Push(field)
        | _ -> ()

let reflectionFind (value: obj) =
    let mutable dependencies = Set.empty
    visitExpressions (fun expr -> dependencies <- dependencies + find expr) value
    dependencies

let reflectionFindProjectReferences (value: obj) =
    let mutable projects = Set.empty
    visitExpressions (fun expr -> projects <- projects + findProjectReferences expr) value
    projects

let reflectionFindAll (value: obj) =
    let mutable dependencies = Set.empty
    let mutable projects = Set.empty
    visitExpressions (fun expr ->
        dependencies <- dependencies + find expr
        projects <- projects + findProjectReferences expr) value
    dependencies, projects
