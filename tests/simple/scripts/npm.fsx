#if !TERRABUILD_SCRIPT
#r "../../../src/Terrabuild/bin/Debug/net9.0/Terrabuild.Scripting.dll"
#endif

open Terrabuild.Scripting
open Terrabuild.ScriptingContracts


module String =
    open System.Text.RegularExpressions

    let (|Regex|_|) pattern input =
        let m = Regex.Match(input, pattern)
        if m.Success then Some(List.tail [ for g in m.Groups -> g.Value ])
        else None


module NpmHelpers =
    open System.Text.Json.Serialization
    open System.Text.Json
    open System.Collections.Generic

    [<CLIMutable>]
    type Package = {
        [<JsonPropertyName("dependencies")>]
        Dependencies: Dictionary<string, string>

        [<JsonPropertyName("devDependencies")>]
        DevDependencies: Dictionary<string, string>
    }

    let findProjectFile (directory: string) =
        let projects =
            System.IO.Directory.EnumerateFiles(directory, "package.json")
            |> List.ofSeq
        match projects with
        | [ project ] -> project
        | [] -> failwith "No project found"
        | _ -> failwith "Multiple projects found"

    let findDependencies (projectFile: string) =
        let json = System.IO.File.ReadAllText projectFile
        let package = JsonSerializer.Deserialize<Package>(json)

        let dependencies = seq {
            if package.Dependencies|> isNull |> not then
                for (KeyValue(_, value)) in package.Dependencies do
                    match value with
                    | String.Regex "^file:(.*)$" [project] -> yield project
                    | _ -> ()

            if package.DevDependencies|> isNull |> not then
                for (KeyValue(_, value)) in package.DevDependencies do
                    match value with
                    | String.Regex "^file:(.*)$" [project] -> yield project
                    | _ -> ()
        }
        Set dependencies





let __defaults__ (context: ExtensionContext) =
    let projectFile = NpmHelpers.findProjectFile context.Directory
    let dependencies = projectFile |> NpmHelpers.findDependencies 
    let projectInfo = 
        { ProjectInfo.Default with
            Ignores = Set [ "node_modules/" ]
            Outputs = Set [ "dist/" ]
            Dependencies = dependencies }
    projectInfo


let __dispatch__ (context: ActionContext) (arguments: string option) =
    let cmd = context.Command
    let arguments = arguments |> Option.defaultValue ""

    let ops = [
        shellOp "npm" "ci"
        shellOp "npm" $"run {cmd} -- {arguments}"   
    ]
    execRequest Cacheability.Always ops


let install (context: ActionContext) =
    let ops = [ shellOp "npm" "ci" ]
    execRequest Cacheability.Always ops


let build (context: ActionContext) (arguments: string option) =
    let args = arguments |> Option.defaultValue ""

    let ops = [
        shellOp "npm" "ci"
        shellOp "npm" $"run build -- {args}"   
    ]
    execRequest Cacheability.Always ops


let test (context: ActionContext) (arguments: string option) =
    let args = arguments |> Option.defaultValue ""

    let ops = [
        shellOp "npm" "ci"
        shellOp "npm" $"run test -- {args}"   
    ]
    execRequest Cacheability.Always ops

let run (context: ActionContext) (command: string) (arguments: string option) =
    let args = arguments |> Option.defaultValue ""

    let ops = [
        shellOp "npm" $"run {command} -- {args}"
    ]
    execRequest Cacheability.Always ops
