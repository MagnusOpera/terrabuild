module Terrabuild.Configuration.Transpiler.Workspace
open Errors
open Terrabuild.Expressions
open Common
open Terrabuild.Lang.AST
open Terrabuild.Configuration.AST
open Terrabuild.Configuration.AST.Workspace
open Collections
open Helpers

type WorkspaceBuilder =
    { Workspace: WorkspaceBlock option
      Targets: Map<string, TargetBlock>
      Variables: Map<string, Expr option>
      Locals: Map<string, Expr>
      Extensions: Map<string, ExtensionBlock> }


let (|Workspace|Target|Variable|Locals|Extension|UnknownBlock|) (block: Block) =
    match block.Resource, block.Id with
    | "workspace", None -> Workspace
    | "target", Some name -> Target name
    | "variable", Some name -> Variable name
    | "locals", None -> Locals
    | "extension", Some name -> Extension name
    | _ -> UnknownBlock


let toWorkspace (block: Block) =
    block
    |> checkAllowedAttributes ["id"; "ignores"; "version"; "engine"; "configuration"; "environment"]
    |> checkNoNestedBlocks
    |> ignore

    let id =
        block |> tryFindAttribute "id"
        |> Option.bind (Eval.asStringOption << simpleEval)
    let version =
        block |> tryFindAttribute "version"
        |> Option.bind (Eval.asStringOption << simpleEval)
    let ignores =
        block |> tryFindAttribute "ignores"
        |> Option.bind (Eval.asStringSetOption << simpleEval)
    let engine =
        match block |> tryFindAttribute "engine" with
        | None -> None
        | Some expr ->
            match expr |> simpleEval |> Eval.asEnum with
            | Ok engine -> engine |> Some
            | _ -> raiseTypeError "Expecting enum for engine attribute"
    let configuration =
        block |> tryFindAttribute "configuration"
        |> Option.bind (Eval.asStringOption << simpleEval)
    let environment =
        block |> tryFindAttribute "environment"
        |> Option.bind (Eval.asStringOption << simpleEval)

    { WorkspaceBlock.Id = id
      WorkspaceBlock.Ignores = ignores
      WorkspaceBlock.Version = version
      WorkspaceBlock.Engine = engine
      WorkspaceBlock.Configuration = configuration
      WorkspaceBlock.Environment = environment }


let toTarget (block: Block) =
    block
    |> checkAllowedAttributes ["depends_on"; "build"; "artifacts"; "group"]
    |> checkNoNestedBlocks
    |> ignore

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
    let group = block |> tryFindAttribute "group"
    { TargetBlock.DependsOn = dependsOn
      TargetBlock.Build = build
      TargetBlock.Cache = cache
      TargetBlock.Group = group }

    
let toVariable (block: Block) =
    block
    |> checkAllowedAttributes ["default"; "description"]
    |> checkNoNestedBlocks
    |> ignore

    let value = block |> tryFindAttribute "default"
    let description = block |> tryFindAttribute "description"
    value


let toLocals (block: Block) =
    block
    |> checkNoNestedBlocks
    |> ignore

    let locals =
        block.Attributes
        |> List.map (fun a -> (a.Name, a.Value))
        |> Map.ofList
    locals


let transpile (blocks: Block list) =
    let rec buildWorkspace (blocks: Block list) (builder: WorkspaceBuilder) =
        match blocks with
        | [] ->
            let workspace =
                match builder.Workspace with
                | None -> { WorkspaceBlock.Id = None
                            WorkspaceBlock.Version = None
                            WorkspaceBlock.Ignores = None
                            WorkspaceBlock.Engine = None
                            WorkspaceBlock.Configuration = None
                            WorkspaceBlock.Environment = None }
                | Some workspace -> workspace

            { WorkspaceFile.Workspace = workspace
              WorkspaceFile.Targets = builder.Targets
              WorkspaceFile.Variables = builder.Variables
              WorkspaceFile.Locals = builder.Locals
              WorkspaceFile.Extensions = builder.Extensions }

        | block::blocks ->
            match block with
            | Workspace ->
                if builder.Workspace <> None then raiseParseError "multiple workspace declared"
                let workspace = toWorkspace block
                buildWorkspace blocks { builder with Workspace = Some workspace }

            | Target name ->
                if builder.Targets.ContainsKey name then raiseParseError $"duplicated target '{name}'"
                let target = toTarget block
                buildWorkspace blocks { builder with Targets = builder.Targets |> Map.add name target }

            | Variable name ->
                if builder.Variables.ContainsKey name then raiseParseError $"duplicated variable '{name}'"
                let variable = toVariable block
                buildWorkspace blocks { builder with Variables = builder.Variables |> Map.add name variable }

            | Locals ->
                let locals = toLocals block
                locals
                |> Map.iter (fun name _ ->
                    if builder.Locals |> Map.containsKey name then raiseParseError $"duplicated local '{name}'")
                buildWorkspace blocks { builder with Locals = builder.Locals |> Map.addMap locals }

            | Extension name ->
                if builder.Extensions.ContainsKey name then raiseParseError $"duplicated extension '{name}'"
                let extension = toExtension block
                buildWorkspace blocks { builder with Extensions = builder.Extensions |> Map.add name extension }

            | UnknownBlock -> raiseParseError $"unexpected block '{block.Resource}'"

    let builder =
        { Workspace = None
          Targets = Map.empty
          Variables = Map.empty
          Locals = Map.empty
          Extensions = Map.empty }
    buildWorkspace blocks builder

