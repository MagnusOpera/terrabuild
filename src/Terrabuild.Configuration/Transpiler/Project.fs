module Terrabuild.Configuration.Transpiler.Project
open Terrabuild.Lang.AST
open Terrabuild.Configuration.AST
open Terrabuild.Configuration.AST.Project
open Errors
open Terrabuild.Expressions
open Common
open Collections
open Helpers


type ProjectBuilder =
    { Project: ProjectBlock option
      Extensions: Map<string, ExtensionBlock>
      Targets: Map<string, TargetBlock>
      Locals: Map<string, Expr> }


let (|Project|Extension|Target|Locals|UnknownBlock|) (block: Block) =
    match block.Resource, block.Id with
    | "project", _ -> Project
    | "extension", Some name -> Extension name
    | "target", Some name -> Target name
    | "locals", None -> Locals
    | _ -> UnknownBlock


let toProject (block: Block) =
    block
    |> checkAllowedAttributes ["depends_on"; "dependencies"; "outputs"; "ignores"; "includes"; "labels"; "environments"]
    |> ignore

    let dependsOn =
        block
        |> tryFindAttribute "depends_on"
        |> Option.map Dependencies.findArrayOfDependencies
        |> Option.map (fun dependsOn ->
            dependsOn |> Set.map (fun dependency ->
                match dependency with
                | String.Regex "^project\.(.*)$" [_] -> dependency
                | _ -> raiseInvalidArg $"Invalid project dependency '{dependency}'"))
    let dependencies = block |> tryFindAttribute "dependencies"
    let outputs = block |> tryFindAttribute "outputs"
    let ignores = block |> tryFindAttribute "ignores"
    let includes = block |> tryFindAttribute "includes"

    let initializers =
        block.Blocks
        |> List.map (fun block ->
            block
            |> checkNoId
            |> ignore
            block.Resource)
        |> Set.ofList
    if initializers.Count <> block.Blocks.Length then raiseInvalidArg $"Duplicated initializers detected"

    let labels =
        block |> tryFindAttribute "labels"
        |> Option.bind (Eval.asStringSetOption << simpleEval)
        |> Option.defaultValue Set.empty

    let environments = block |> tryFindAttribute "environments"

    { ProjectBlock.Initializers = initializers
      ProjectBlock.Id = block.Id
      ProjectBlock.DependsOn = dependsOn
      ProjectBlock.Dependencies = dependencies
      ProjectBlock.Outputs = outputs
      ProjectBlock.Ignores = ignores
      ProjectBlock.Includes = includes
      ProjectBlock.Labels = labels
      ProjectBlock.Environments = environments }


let toTarget (block: Block) =
    block
    |> checkAllowedAttributes ["outputs"; "depends_on"; "build"; "artifacts"]
    |> ignore

    let outputs = block |> tryFindAttribute "outputs"
    let dependsOn =
        block |> tryFindAttribute "depends_on"
        |> Option.map Dependencies.findArrayOfDependencies
        |> Option.map (fun dependsOn ->
            dependsOn |> Set.map (fun dependency ->
                match dependency with
                | String.Regex "^target\.(.*)$" [targetIdentifier] -> targetIdentifier
                | _ -> raiseInvalidArg $"Invalid target dependency '{dependency}'"))
    let build = block |> tryFindAttribute "build"
    let cache = block |> tryFindAttribute "artifacts"
    let steps =
        block.Blocks
        |> List.map (fun step ->
            step
            |> checkNoNestedBlocks
            |> ignore

            let command =
                match step.Id with
                | Some name -> name
                | _ ->raiseParseError "command is not declared"

            let parameters =
                step.Attributes
                |> List.map (fun a -> (a.Name, a.Value))
                |> Map.ofList

            { Extension = step.Resource
              Command = command
              Parameters = parameters })

    { TargetBlock.Outputs = outputs
      TargetBlock.DependsOn = dependsOn
      TargetBlock.Build = build
      TargetBlock.Cache = cache
      TargetBlock.Steps = steps }



let transpile (blocks: Block list) =
    let rec buildProject (blocks: Block list) (builder: ProjectBuilder) =
        match blocks with
        | [] ->
            let project =
                match builder.Project with
                | None -> { ProjectBlock.Id = None
                            ProjectBlock.Initializers = Set.empty
                            ProjectBlock.DependsOn = None
                            ProjectBlock.Dependencies = None
                            ProjectBlock.Outputs = None
                            ProjectBlock.Ignores = None
                            ProjectBlock.Includes = None
                            ProjectBlock.Labels = Set.empty
                            ProjectBlock.Environments = None }
                | Some workspace -> workspace

            { ProjectFile.Project = project
              ProjectFile.Extensions = builder.Extensions
              ProjectFile.Targets = builder.Targets
              ProjectFile.Locals = builder.Locals }

        | block::blocks ->
            match block with
            | Project ->
                if builder.Project <> None then raiseParseError "multiple project declared"
                let project = toProject block
                buildProject blocks { builder with Project = Some project }

            | Extension name ->
                if builder.Extensions.ContainsKey name then raiseParseError $"duplicated extension '{name}'"
                let extension = toExtension block
                buildProject blocks { builder with Extensions = builder.Extensions |> Map.add name extension }

            | Target name ->
                if builder.Targets.ContainsKey name then raiseParseError $"duplicated target '{name}'"
                let target = toTarget block
                buildProject blocks { builder with Targets = builder.Targets |> Map.add name target }

            | Locals ->
                let locals = toLocals block
                locals
                |> Map.iter (fun name _ ->
                    if builder.Locals |> Map.containsKey name then raiseParseError $"duplicated local '{name}'")
                buildProject blocks { builder with Locals = builder.Locals |> Map.addMap locals }

            | UnknownBlock -> raiseParseError $"unexpected block '{block.Resource}'"

    let builder =
        { Project = None
          Extensions = Map.empty
          Targets = Map.empty 
          Locals = Map.empty }
    buildProject blocks builder

