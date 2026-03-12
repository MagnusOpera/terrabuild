namespace Terrabuild.Lang.AST
open Terrabuild.Expression

type [<RequireQualifiedAccess>] AssignmentOperator =
    | Assign
    | Add
    | Remove

type [<RequireQualifiedAccess>] Attribute =
    { Name: string
      Operator: AssignmentOperator
      Value: Expr }
with
    static member Build name operator value =
        { Attribute.Name = name
          Attribute.Operator = operator
          Attribute.Value = value }

    static member Append (attributes: Attribute list) (attribute: Attribute) =
        if attribute.Name <> "outputs"
           && attribute.Name <> "depends_on"
           && attributes |> List.exists (fun a -> a.Name = attribute.Name) then
            Errors.raiseParseError $"duplicated attribute '{attribute.Name}'"
        else
            attributes @ [attribute]


type [<RequireQualifiedAccess>] Block =
    { Resource: string
      Id: string option
      Attributes: Attribute list
      Blocks: Block list }
with
    static member Build resource id (attributes, blocks) =
        { Block.Resource = resource
          Block.Id = id
          Block.Attributes = attributes
          Block.Blocks = blocks }


type [<RequireQualifiedAccess>] File =
    { Blocks: Block list }
with
    static member Build blocks =
        { File.Blocks = blocks }
