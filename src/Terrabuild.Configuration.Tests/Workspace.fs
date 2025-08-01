module Terrabuild.Configuration.Tests.Workspace
open System.IO
open NUnit.Framework
open FsUnit

open Terrabuild.Configuration.AST
open Terrabuild.Configuration.AST.Workspace
open Terrabuild.Expressions


[<Test>]
let parseWorkspace() =
    let expectedWorkspace =
        let targetBuild = 
            { TargetBlock.DependsOn = Set [ "^build" ] |> Some
              TargetBlock.Rebuild = None
              TargetBlock.Ephemeral = None
              TargetBlock.Restore = Expr.False |> Some }
        let targetDist =
            { TargetBlock.DependsOn = Set [ "build" ] |> Some
              TargetBlock.Rebuild = Expr.Bool true |> Some
              TargetBlock.Ephemeral = Expr.False |> Some
              TargetBlock.Restore = None }
        let targetDummy =
            { TargetBlock.DependsOn = None
              TargetBlock.Rebuild = None
              TargetBlock.Ephemeral = None
              TargetBlock.Restore = None }

        let extDotnet =
            { Container = Some (Expr.String "mcr.microsoft.com/dotnet/sdk:8.0.101")
              Platform = None
              Variables = None
              Script = None
              Defaults = Map [ "configuration", Expr.Variable "local.configuration" ] |> Some }
        let extDocker =
            { Container = None
              Platform = None
              Variables = None
              Script = None
              Defaults = None }
        let extNpm =
            { Container = Some (Expr.String "node:20")
              Platform = None
              Variables = None
              Script = "scripts/npm.fsx" |> Expr.String |> Some
              Defaults = None }

        { WorkspaceFile.Workspace = { Id = "d7528db2-83e0-4164-8c8e-1e0d6d6357ca" |> Some
                                      Ignores = Set [ "**/node_modules" ] |> Some
                                      Version = None }
          WorkspaceFile.Targets = Map [ "build", targetBuild
                                        "dist", targetDist
                                        "dummy", targetDummy ]
          WorkspaceFile.Variables = Map.empty
          WorkspaceFile.Locals = Map.empty
          WorkspaceFile.Extensions = Map [ "dotnet", extDotnet
                                           "docker", extDocker
                                           "npmext", extNpm ] }


    let content = File.ReadAllText("TestFiles/Success_WORKSPACE")
    let workspace = Terrabuild.Configuration.FrontEnd.Workspace.parse content

    workspace
    |> should equal expectedWorkspace

[<Test>]
let parseWorkspace2() =
    let expectedWorkspace =
        let targetBuild = 
            { TargetBlock.DependsOn = Set [ "^build" ] |> Some
              TargetBlock.Rebuild = None
              TargetBlock.Ephemeral = None
              TargetBlock.Restore = None }
        let targetDist =
            { TargetBlock.DependsOn = Set [ "build" ] |> Some
              TargetBlock.Rebuild = Expr.Bool true |> Some
              TargetBlock.Ephemeral = None
              TargetBlock.Restore = None }
        let targetDummy =
            { TargetBlock.DependsOn = None
              TargetBlock.Rebuild = None
              TargetBlock.Ephemeral = None
              TargetBlock.Restore = None }

        let extDotnet =
            { Container = Expr.String "mcr.microsoft.com/dotnet/sdk:8.0.101" |> Some
              Platform = None
              Variables = None
              Script = None
              Defaults = Map [ "configuration1", Expr.Function (Function.Item, [Expr.Variable "var.map"; Expr.String "toto"])
                               "configuration2", Expr.Function (Function.Item, [Expr.Variable "var.map"; Expr.String "titi"])
                               "configuration3", Expr.Function (Function.Replace, [Expr.String "toto titi"; Expr.String "toto"; Expr.String "titi"]) ] |> Some }
        let extDocker =
            { Container = None
              Platform = None
              Variables = None
              Script = None
              Defaults = None }

        { WorkspaceFile.Workspace = { Id = None; Ignores = None; Version = None }
          WorkspaceFile.Targets = Map [ "build", targetBuild
                                        "dist", targetDist
                                        "dummy", targetDummy ]
          WorkspaceFile.Variables = Map.empty
          WorkspaceFile.Locals = Map.empty
          WorkspaceFile.Extensions = Map [ "dotnet", extDotnet
                                           "docker", extDocker ] }


    let content = File.ReadAllText("TestFiles/Success_WORKSPACE2")
    let workspace = Terrabuild.Configuration.FrontEnd.Workspace.parse content

    workspace
    |> should equal expectedWorkspace


[<Test>]
let unexpectedAttributeIsError() =
    let content = File.ReadAllText("TestFiles/Error_UnexpectedAttribute")
    (fun () -> Terrabuild.Configuration.FrontEnd.Workspace.parse content |> ignore) |> should (throwWithMessage "unexpected attribute 'tagada'") typeof<Errors.TerrabuildException>

[<Test>]
let unexpectedBlockIsError() =
    let content = File.ReadAllText("TestFiles/Error_UnexpectedBlock")
    (fun () -> Terrabuild.Configuration.FrontEnd.Workspace.parse content |> ignore) |> should (throwWithMessage "unexpected block 'tagada'") typeof<Errors.TerrabuildException>

[<Test>]
let unexpectedNestedBlockIsError() =
    let content = File.ReadAllText("TestFiles/Error_UnexpectedNestedBlock")
    (fun () -> Terrabuild.Configuration.FrontEnd.Workspace.parse content |> ignore) |> should (throwWithMessage "unexpected nested block 'toto'") typeof<Errors.TerrabuildException>

[<Test>]
let duplicatedExtensionIsError() =
    let content = File.ReadAllText("TestFiles/Error_DuplicatedExtension")
    (fun () -> Terrabuild.Configuration.FrontEnd.Workspace.parse content |> ignore) |> should (throwWithMessage "duplicated extension '@dotnet'") typeof<Errors.TerrabuildException>

[<Test>]
let duplicatedTargetIsError() =
    let content = File.ReadAllText("TestFiles/Error_Workspace_DuplicatedTarget")
    (fun () -> Terrabuild.Configuration.FrontEnd.Workspace.parse content |> ignore) |> should (throwWithMessage "duplicated target 'build'") typeof<Errors.TerrabuildException>

[<Test>]
let duplicatedLocalIsError() =
    let content = File.ReadAllText("TestFiles/Error_DuplicatedLocal")
    (fun () -> Terrabuild.Configuration.FrontEnd.Workspace.parse content |> ignore) |> should (throwWithMessage "duplicated local 'app_name'") typeof<Errors.TerrabuildException>

[<Test>]
let duplicatedVariableIsError() =
    let content = File.ReadAllText("TestFiles/Error_Workspace_DuplicatedVariable")
    (fun () -> Terrabuild.Configuration.FrontEnd.Workspace.parse content |> ignore) |> should (throwWithMessage "duplicated variable 'configuration'") typeof<Errors.TerrabuildException>
