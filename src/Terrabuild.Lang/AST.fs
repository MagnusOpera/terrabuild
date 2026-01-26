namespace Terrabuild.Lang.AST
open Terrabuild.Expressions


type [<RequireQualifiedAccess>] Attribute =
    { Name: string
      Value: Expr }
with
    static member Build name value =
        { Attribute.Name = name
          Attribute.Value = value }

    static member Append (attributes: Attribute list) (attribute: Attribute) =
        if attributes |> List.exists (fun a -> a.Name = attribute.Name) then
            Errors.reportParseError $"duplicated attribute '{attribute.Name}'"
            attributes
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
