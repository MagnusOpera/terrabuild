module private Terrabuild.Configuration.Transpiler.Common
open Terrabuild.Lang.AST
open Terrabuild.Configuration.AST
open Helpers

let toExtension (block: Block) =
    block
    |> checkAllowedAttributes ["image"; "platform"; "variables"; "script"; "defaults"; "cpus"]
    |> checkAllowedNestedBlocks ["defaults"; "env"]
    |> ignore

    let image = block |> tryFindAttribute "image"
    let platform = block |> tryFindAttribute "platform"
    let variables = block |> tryFindAttribute "variables"
    let script = block |> tryFindAttribute "script"
    let cpus = block |> tryFindAttribute "cpus"
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

    let env =
        block
        |> tryFindBlock "env"
        |> Option.map (fun envs ->
            envs
            |> checkNoNestedBlocks
            |> ignore

            envs.Attributes
            |> List.map (fun a -> (a.Name, a.Value))
            |> Map.ofList)

    { ExtensionBlock.Image = image
      ExtensionBlock.Platform = platform
      ExtensionBlock.Variables = variables
      ExtensionBlock.Script = script
      ExtensionBlock.Cpus = cpus
      ExtensionBlock.Defaults = defaults
      ExtensionBlock.Env = env } 

let toLocals (block: Block) =
    block
    |> checkNoNestedBlocks
    |> ignore

    let variables = block.Attributes
                    |> List.map (fun a -> (a.Name, a.Value))
                    |> Map.ofList
    variables


