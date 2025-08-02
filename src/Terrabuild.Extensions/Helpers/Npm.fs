module NpmHelpers

open System.Text.Json.Serialization
open System.Collections.Generic
open Errors

[<CLIMutable>]
type Package = {
    [<JsonPropertyName("dependencies")>]
    Dependencies: Dictionary<string, string> option

    [<JsonPropertyName("devDependencies")>]
    DevDependencies: Dictionary<string, string> option
}

let findProjectFile (directory: string) =
    let projects =
        System.IO.Directory.EnumerateFiles(directory, "package.json")
        |> List.ofSeq
    match projects with
    | [ project ] -> project
    | [] -> raiseInvalidArg "No project found"
    | _ -> raiseInvalidArg "Multiple projects found"

let findDependencies (projectFile: string) =
    let json = IO.readTextFile projectFile
    let package = Json.Deserialize<Package> json

    let dependencies = seq {
        match package.Dependencies with
        | Some dependencies ->
            for (KeyValue(_, value)) in dependencies do
                match value with
                | String.Regex "^file:(.*)$" [project] -> yield project
                | _ -> ()
        | _ -> ()

        match package.DevDependencies with
        | Some dependencies ->
            for (KeyValue(_, value)) in dependencies do
                match value with
                | String.Regex "^file:(.*)$" [project] -> yield project
                | _ -> ()
        | _ -> ()
    }
    Set dependencies
