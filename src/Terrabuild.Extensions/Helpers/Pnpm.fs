module PnpmHelpers

open System.Text.Json.Serialization
open System.Collections.Generic
open Errors

[<CLIMutable>]
type Package = {
    [<JsonPropertyName("name")>]
    Name: string

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

let findName (projectFile: string) =
    let json = IO.readTextFile projectFile
    let package = Json.Deserialize<Package> json
    package.Name

let findDependencies (projectFile: string) =
    let json = IO.readTextFile projectFile
    let package = Json.Deserialize<Package> json

    let dependencies = seq {
        match package.Dependencies with
        | Some dependencies ->
            for (KeyValue(key, value)) in dependencies do
                match value with
                | String.Regex "^workspace:\*$" [] -> yield key
                | _ -> ()
        | _ -> ()

        match package.DevDependencies with
        | Some dependencies ->
            for (KeyValue(key, value)) in dependencies do
                match value with
                | String.Regex "^workspace:\*$" [] -> yield key
                | _ -> ()
        | _ -> ()
    }
    Set dependencies
