module Terrabuild.Expression.Dependencies
open System.Collections.Generic
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
let reflectionFind (o: obj) =

    let rec reflectionFind (theObject: obj | null) =
        seq {
            match theObject with
            | :? System.Collections.IEnumerable as enumerable ->
                for so in enumerable do
                    yield! reflectionFind so
            | :? Expr as expr -> yield find expr
            | NonNull theObject ->
                let ty = theObject.GetType()
                if ty.IsGenericType then
                    let tyDef = ty.GetGenericTypeDefinition()
                    if tyDef = fsmapTy then
                        let valuesProperty = ty.GetProperty("Values")
                        match valuesProperty with
                        | NonNull valuesProperty ->
                            let values = valuesProperty.GetValue(theObject)
                            yield! reflectionFind values
                        | _ -> ()
                    elif tyDef = fsoptionTy then
                        match ty.GetProperty("Value") with
                        | NonNull valueProperty ->
                            let value = valueProperty.GetValue(theObject, null)
                            yield! reflectionFind value
                        | _ -> ()
                    elif tyDef = fskvpTy then
                        match ty.GetProperty("Value") with
                        | NonNull valueProperty ->
                            let value = valueProperty.GetValue(theObject, null)
                            yield! reflectionFind value
                        | _ -> ()
                elif FSharpType.IsRecord(ty, false) then
                    let fields = FSharpType.GetRecordFields(ty) |> Array.map (fun propInfo -> propInfo.GetValue(theObject, null))
                    for field in fields do
                        yield! reflectionFind field
            | _ -> ()
        }

    o |> reflectionFind |> Seq.fold (fun acc s -> acc + s) Set.empty

let reflectionFindProjectReferences (o: obj) =

    let rec reflectionFindProjectReferences (theObject: obj | null) =
        seq {
            match theObject with
            | :? System.Collections.IEnumerable as enumerable ->
                for so in enumerable do
                    yield! reflectionFindProjectReferences so
            | :? Expr as expr -> yield findProjectReferences expr
            | NonNull theObject ->
                let ty = theObject.GetType()
                if ty.IsGenericType then
                    let tyDef = ty.GetGenericTypeDefinition()
                    if tyDef = fsmapTy then
                        let valuesProperty = ty.GetProperty("Values")
                        match valuesProperty with
                        | NonNull valuesProperty ->
                            let values = valuesProperty.GetValue(theObject)
                            yield! reflectionFindProjectReferences values
                        | _ -> ()
                    elif tyDef = fsoptionTy then
                        match ty.GetProperty("Value") with
                        | NonNull valueProperty ->
                            let value = valueProperty.GetValue(theObject, null)
                            yield! reflectionFindProjectReferences value
                        | _ -> ()
                    elif tyDef = fskvpTy then
                        match ty.GetProperty("Value") with
                        | NonNull valueProperty ->
                            let value = valueProperty.GetValue(theObject, null)
                            yield! reflectionFindProjectReferences value
                        | _ -> ()
                elif FSharpType.IsRecord(ty, false) then
                    let fields = FSharpType.GetRecordFields(ty) |> Array.map (fun propInfo -> propInfo.GetValue(theObject, null))
                    for field in fields do
                        yield! reflectionFindProjectReferences field
            | _ -> ()
        }

    o |> reflectionFindProjectReferences |> Seq.fold (fun acc s -> acc + s) Set.empty
