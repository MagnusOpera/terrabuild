module private Terrabuild.Configuration.Transpiler.Helpers
open Terrabuild.Expression
open Errors
open Terrabuild.Lang.AST
open Terrabuild.Configuration.AST

let checkNoId (block: Block) =
    match block.Id with
    | None -> block
    | Some id -> raiseParseError $"unexpected id '{id}'"

let checkAllowedAttributes (allowed: string list) (block: Block) =
    block.Attributes
    |> List.iter (fun a ->
        if not (allowed |> List.contains a.Name) then raiseParseError $"unexpected attribute '{a.Name}'")
    block

let checkAllowedAttributeOperators (allowedAugmentedAttributes: string list) (block: Block) =
    block.Attributes
    |> List.iter (fun a ->
        if a.Operator <> AssignmentOperator.Assign && not (allowedAugmentedAttributes |> List.contains a.Name) then
            raiseParseError $"attribute '{a.Name}' does not support operator '{a.Operator}'")
    block

let checkAllowedNestedBlocks (allowed: string list) (block: Block) =
    block.Blocks
    |> List.iter (fun b ->
        if not (allowed |> List.contains b.Resource) then raiseParseError $"unexpected nested block '{b.Resource}'")
    block

let checkNoNestedBlocks = checkAllowedNestedBlocks []

let tryFindAttribute (name: string) (block: Block) =
    block.Attributes
    |> List.tryFind (fun a -> a.Name = name)
    |> Option.map (fun a ->
        if a.Operator <> AssignmentOperator.Assign then
            raiseParseError $"attribute '{a.Name}' does not support operator '{a.Operator}'"
        a.Value)

let findOutputOperations (block: Block) =
    block.Attributes
    |> List.choose (fun a ->
        if a.Name = "outputs" then
            Some {
                OutputOperation.Operator = a.Operator
                OutputOperation.Value = a.Value
            }
        else
            None)

let tryFindBlock (resource: string) (block: Block) =
    let candidates = 
        block.Blocks
        |> List.choose (fun b -> if b.Resource = resource then Some b else None)
    match candidates with
    | [] -> None
    | [block] -> Some block
    | _ -> raiseParseError $"multiple {resource} declared"

let valueOrDefault (defaultValue: Expr) (attribute: Attribute option) =
    match attribute with
    | Some attribute -> attribute.Value
    | None -> defaultValue

let simpleEval = Eval.eval Eval.EvaluationContext.Empty
