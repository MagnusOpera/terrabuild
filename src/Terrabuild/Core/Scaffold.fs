module Scalffold
open System
open System.IO
open Collections
open Microsoft.Extensions.FileSystemGlobbing
open Errors


// [<RequireQualifiedAccess>]
type ExtensionType =
    | Dotnet
    | Gradle
    | Npm
    | Make
    | Docker
    | Terraform
    | Cargo

// [<RequireQualifiedAccess>]
type Goal =
    | Install
    | Build
    | Test
    | Publish
    | Plan
    | Apply


type Project = {
    Directory: string
    Type: ExtensionType
    Others: ExtensionType list
}



type Extension = {
    Container: string option
    Defaults: Map<string, string>
    Actions: Map<Goal, string list>
}


let targetConfigs =
    Map [
        Install, ([], [])
        Build, ([ "cache = \"local\"" ], [ "target.install"; "target.^build" ])
        Test, ([], [ "target.install" ])
        Publish, ([], [ "target.build" ])
        Plan, ([ "rebuild = terrabuild.retry" ], [ "target.install"; "target.^plan"; "target.publish" ])
        Apply, ([], [ "target.^apply"; "target.plan" ])
    ]

let localConfigs =
    Map [
        "config", "terrabuild.configuration ? \"Debug\" : \"Release\""
    ]


// NOTE: order is important, first found is main extension
let extMarkers = [
    Dotnet, "*.*proj"
    Gradle, "build.gradle"
    Npm, "package.json"
    Make, "Makefile"
    Docker, "Dockerfile"
    Terraform, ".terraform.lock.hcl"
    Cargo, "Cargo.toml"
]


let extConfigs =
    Map [ 
        Dotnet, { Container = None //Some "mcr.microsoft.com/dotnet/sdk:8.0"
                  Defaults = Map [ "configuration", "local.config" ]
                  Actions = Map [ Install, [ "restore"]
                                  Build, [ "build" ]
                                  Publish, [ "publish" ]
                                  Test, [ "test" ] ] }

        Gradle, { Container = None //Some "gradle:jdk21"
                  Defaults = Map [ "configuration", "local.configuration" ]
                  Actions = Map [ Build, [ "build" ] ] }

        Npm, { Container = None //Some "node:20"
               Defaults = Map.empty
               Actions = Map [ Install, [ "install" ]
                               Build, [ "build" ]
                               Test, [ "test" ] ] }

        Make, { Container = None
                Defaults = Map.empty
                Actions = Map [ Build, [ "build" ] ] }

        Docker, { Container = None //Some "docker:25.0"
                  Defaults = Map [ "image", "\"ghcr.io/example/${terrabuild.project_slug}\""
                                   "arguments", "{ configuration: local.config }" ]
                  Actions = Map [ Publish, [ "build" ] ] }
  
        Terraform, { Container = None //Some "hashicorp/terraform:1.7"
                     Defaults = Map.empty
                     Actions = Map [
                         Install, [ "init" ]
                         Plan, [ "plan" ]
                         Apply, [ "apply" ] ] }
 
        Cargo, { Container = None // Some "rust:1.79.0"
                 Defaults = Map [ "profile", "local.configuration" ]
                 Actions = Map [ Build, [ "build" ] ] }
    ]




let rec findProjectInDir dir =
    seq {
        let getFiles m = Directory.EnumerateFiles(dir, m)
        let isProject x = x |> Seq.isEmpty |> not

        let projects =
            extMarkers
            |> List.choose (fun (ext, pattern) ->
                if getFiles pattern |> isProject then 
                    { Directory = dir
                      Type = ext
                      Others = [] } |> Some
                else
                    None)

        match projects with
        | [ project ] ->
            yield project
        | mainProject :: others ->
            yield { mainProject 
                    with Others = others |> List.map _.Type }
        | _ ->
            for dir in Directory.EnumerateDirectories(dir) do
                yield! findProjectInDir dir
    }


let toLower s = $"{s}".ToLowerInvariant()
let toExtension (pt: ExtensionType) = pt |> toLower

let genWorkspace (extensions: ExtensionType set) =
    seq {
        for (KeyValue(target, (attributes, dependsOn))) in targetConfigs do
            ""
            $"target {target |> toLower} {{"
            let listDependsOn = String.concat " " dependsOn
            for attribute in attributes do $"  {attribute}"
            $"  depends_on = [ {listDependsOn} ]"
            "}"

        ""
        $"locals {{"
        for (KeyValue(env, value)) in localConfigs do
            $"  {env} = {value}"
        "}"

        for extension in extensions do
            let config = extConfigs |> Map.find extension
            let container = config.Container
            let variables = config.Defaults
            let declare = container <> None || variables <> Map.empty
            if declare then
                ""
                $"extension @{extension |> toExtension} {{"
                match container with
                | Some container ->
                    $"  container = \"{container}\""
                | _ -> ()

                if variables <> Map.empty then
                    "  defaults {"
                    for (KeyValue(key, value)) in variables do
                        $"    {key} = {value}"
                    "  }"

                "}"
    }




let genProject (project: Project) =
    let extensions = project.Type :: project.Others

    seq {
        // generate project block with default initializer
        match extensions with
        | main :: others when others <> [] ->
            let exts = others |> Seq.map toExtension |> String.join " "
            yield $"# WARNING: other project types detected: {exts}"
        | _ -> ()
        yield $"project {{"
        yield $"  @{project.Type |> toExtension} {{ }}"
        yield "}"

        // generate targets
        let allCommands =
            extensions
            |> List.collect (fun ext ->
                extConfigs[ext].Actions
                |> Seq.map (fun kvp -> kvp.Key, ext, kvp.Value) |> List.ofSeq)
            |> List.collect (fun (targetType, ext, cmds) -> cmds |> List.map (fun cmd -> targetType, (ext, cmd)))
            |> List.groupBy (fun (targetType, _) -> targetType)
            |> Map.ofList
            |> Map.map (fun _ l -> l |> List.map snd)

        for (KeyValue(targetType, cmds)) in allCommands do
            yield ""
            yield $"target {targetType |> toLower} {{"
            for (projType, cmd) in cmds do
                yield $"    @{projType |> toExtension} {cmd} {{ }}"
            yield "}"
    }

let findBut (file: string) (rootDir: string) =
    let excludes = [ "**/node_modules/"; "**/bin/"; "**/obj/"; "**/build/classes/" ]

    let matcher = Matcher()
    matcher.AddInclude(file).AddExcludePatterns(excludes)
    let result =
        matcher.GetResultsInFullPath(rootDir)
        |> List.ofSeq
    result

let findWorkspace = findBut "**/WORKSPACE"
let findProject = findBut "**/PROJECT"

let scaffold workspaceDir force =
    // check we won't override files first
    if force |> not then
        let workspaceExists = workspaceDir |> findWorkspace |> Seq.tryHead
        let projectExists = workspaceDir |> findProject |> Seq.tryHead

        match workspaceExists, projectExists with
        | Some file, _ -> raiseInvalidArg $"WORKSPACE file found '{file}'"
        | _, Some file -> raiseInvalidArg $"PROJECT file found '{file}'"
        | _ -> ()

    let projects = findProjectInDir workspaceDir |> List.ofSeq

    projects
    |> Seq.iter (fun project ->
        printfn $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} PROJECT {project.Directory}"
        let projectFile = FS.combinePath project.Directory "PROJECT"
        let projectContent = project |> genProject
        File.WriteAllLines(projectFile, projectContent)
    )

    let mainExtensions =
        projects
        |> List.map (fun p -> p.Type)
    let otherExtensions =
        projects
        |> List.collect (fun p -> p.Others )
    let extensions = mainExtensions @ otherExtensions |> Set

    printfn $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} WORKSPACE"
    let workspaceFile = FS.combinePath workspaceDir "WORKSPACE"
    let workspaceContent = genWorkspace extensions
    File.WriteAllLines(workspaceFile, workspaceContent)
