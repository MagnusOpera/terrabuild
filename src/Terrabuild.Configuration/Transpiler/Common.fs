module private Terrabuild.Configuration.Transpiler.Common
open Terrabuild.Lang.AST
open Terrabuild.Configuration.AST
open Helpers

let toExtension (block: Block) =
    block
    |> checkAllowedAttributes ["container"; "platform"; "variables"; "script"; "defaults"; "batch"]
    |> checkAllowedNestedBlocks ["defaults"]
    |> ignore

    let container = block |> tryFindAttribute "container"
    let platform = block |> tryFindAttribute "platform"
    let variables = block |> tryFindAttribute "variables"
    let script = block |> tryFindAttribute "script"
    let batch = block |> tryFindAttribute "batch"
    let defaults =
        block
        |> tryFindBlock "defaults"
        |> Option.map (fun defaults ->
            defaults
            |> checkNoNestedBlocks
            |> ignore

            defaults.Attributes
            |> List.map (fun a -> (a.Name, a.Value))
            |> Map.ofList)

    { ExtensionBlock.Container = container
      ExtensionBlock.Platform = platform
      ExtensionBlock.Variables = variables
      ExtensionBlock.Script = script
      ExtensionBlock.Defaults = defaults
      ExtensionBlock.Batch = batch } 

let toLocals (block: Block) =
    block
    |> checkNoNestedBlocks
    |> ignore

    let variables = block.Attributes
                    |> List.map (fun a -> (a.Name, a.Value))
                    |> Map.ofList
    variables


