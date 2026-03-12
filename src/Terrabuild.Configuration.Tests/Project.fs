module Terrabuild.Configuration.Tests.Project

open System.IO
open NUnit.Framework
open FsUnit

open Terrabuild.Configuration.AST
open Terrabuild.Configuration.AST.Project

open Terrabuild.Expression
open Terrabuild.Lang.AST

let private outputAssign value =
    { OutputOperation.Operator = AssignmentOperator.Assign
      OutputOperation.Value = value }

let private outputAdd value =
    { OutputOperation.Operator = AssignmentOperator.Add
      OutputOperation.Value = value }

let private outputRemove value =
    { OutputOperation.Operator = AssignmentOperator.Remove
      OutputOperation.Value = value }

let private dependencyAssign value =
    { DependencyOperation.Operator = AssignmentOperator.Assign
      DependencyOperation.Value = value }

let private dependencyAdd value =
    { DependencyOperation.Operator = AssignmentOperator.Add
      DependencyOperation.Value = value }

let private dependencyRemove value =
    { DependencyOperation.Operator = AssignmentOperator.Remove
      DependencyOperation.Value = value }

[<Test>]
let parseProject() =
    let expectedProject =
        let project =
            { ProjectBlock.Type = Some "@dotnet"
              ProjectBlock.Name = Some "id"
              ProjectBlock.Initializers = Set [ "@dotnet" ]
              ProjectBlock.DependsOn = None
              ProjectBlock.Outputs = [ outputAssign (Expr.List [ Expr.String "dist" ]) ]
              ProjectBlock.Ignores = None
              ProjectBlock.Includes = None
              ProjectBlock.Labels = Set [ "app"; "dotnet" ]
              ProjectBlock.Environments = Expr.List [ Expr.String "dev"] |> Some }

        let extDotnet =
            { Image = None
              Platform = None
              Cpus = Expr.Number 2 |> Some
              Variables = None
              Script = None
              Defaults = Map [ "configuration", Expr.Variable "var.configuration" ] |> Some
              Env = None }        
        let extDocker =
            { Image = None
              Platform = None
              Cpus = None
              Variables = Expr.List [ Expr.String "ARM_TENANT_ID" ] |> Some
              Script = None
              Defaults = Map [ "configuration", Expr.Variable "local.configuration"
                               "image", Expr.String "ghcr.io/magnusopera/dotnet-app" ] |> Some
              Env = None }
        let extDummy =
            { Image = None
              Platform = None
              Cpus = None
              Variables = None
              Script = "dummy.fsx" |> Expr.String |> Some
              Defaults = None
              Env = Map [ "DUMMY_VAR", Expr.String "tagada" ] |> Some }

        let targetBuild = 
            { TargetBlock.DependsOn = [ dependencyAssign (Set [ "dist" ]) ]
              TargetBlock.Build = None
              TargetBlock.Outputs = []
              TargetBlock.Cache = None
              TargetBlock.Batch = Expr.Enum "partition" |> Some
              TargetBlock.Steps = [ { Extension = "@dotnet"; Command = "build"; Parameters = Map.empty } ] }
        let targetDist =
            { TargetBlock.DependsOn = []
              TargetBlock.Build = None
              TargetBlock.Outputs = []
              TargetBlock.Cache = None
              TargetBlock.Batch = None
              TargetBlock.Steps = [ { Extension = "@dotnet"; Command = "build"; Parameters = Map.empty }
                                    { Extension = "@dotnet"; Command = "publish"; Parameters = Map.empty } ] }
        let targetDocker =
            { TargetBlock.DependsOn = []
              TargetBlock.Build = "auto" |> Expr.Enum |> Some
              TargetBlock.Outputs = []
              TargetBlock.Cache = "remote" |> Expr.Enum |> Some
              TargetBlock.Batch = None
              TargetBlock.Steps = [ { Extension = "@shell"; Command = "echo"
                                      Parameters = Map [ "arguments", Expr.Function (Function.Trim,
                                                                                     [ Expr.Function (Function.Plus,
                                                                                                      [ Expr.String "building project1 "
                                                                                                        Expr.Variable "local.configuration" ]) ]) ] }
                                    { Extension = "@docker"; Command = "build"
                                      Parameters = Map [ "arguments", Expr.Map (Map [ "config", Expr.String "Release"
                                                                                      "my_variable", Expr.Number 42 ]) ] }
                                    { Extension = "@npm"; Command = "version"
                                      Parameters = Map [ "arguments", Expr.Variable "local.npm_version"
                                                         "version", Expr.String "1.0.0" ] } ] }

        { ProjectFile.Extensions = Map [ "@dotnet", extDotnet
                                         "@docker", extDocker
                                         "dummy", extDummy ]
          ProjectFile.Project = project
          ProjectFile.Targets = Map [ "build", targetBuild
                                      "dist", targetDist
                                      "docker", targetDocker ]
          ProjectFile.Locals = Map.empty }

    let content = File.ReadAllText("TestFiles/Success_PROJECT")
    let project = Terrabuild.Configuration.FrontEnd.Project.parse content

    project
    |> should equal expectedProject
    
[<Test>]
let parseProject2() =
    let expectedProject =
        let project =
            { ProjectBlock.Type = Some "@dotnet"
              ProjectBlock.Name = None
              ProjectBlock.Initializers = Set [ "@dotnet" ]
              ProjectBlock.DependsOn = None
              ProjectBlock.Outputs = []
              ProjectBlock.Ignores = None
              ProjectBlock.Includes = None
              ProjectBlock.Labels = Set.empty
              ProjectBlock.Environments = None }

        let extDotnet =
            { Image = None
              Platform = None
              Cpus = None
              Variables = None
              Script = None
              Defaults = None
              Env = None }        

        let buildTarget = 
            { TargetBlock.Build = "always" |> Expr.Enum |> Some
              TargetBlock.Outputs = [ outputAssign (Expr.List [ Expr.Function (Function.Format,
                                                                                [ Expr.String "{0}{1}"
                                                                                  Expr.Variable "local.wildcard"
                                                                                  Expr.String ".dll" ])]) ]
              TargetBlock.DependsOn = []
              TargetBlock.Cache = None
              TargetBlock.Batch = None
              TargetBlock.Steps = [ { Extension = "@dotnet"; Command = "build"; Parameters = Map.empty } ] }

        let locals = 
            Map [ "app_name", Expr.Function (Function.Plus, 
                                             [ Expr.String "terrabuild"
                                               Expr.Variable "local.terrabuild_project" ]) ]

        { ProjectFile.Extensions = Map [ "@dotnet", extDotnet ]
          ProjectFile.Project = project
          ProjectFile.Targets = Map [ "build", buildTarget ]
          ProjectFile.Locals = locals }

    let content = File.ReadAllText("TestFiles/Success_PROJECT2")
    let project = Terrabuild.Configuration.FrontEnd.Project.parse content

    project
    |> should equal expectedProject



[<Test>]
let unexpectedAttributeIsError() =
    let content = File.ReadAllText("TestFiles/Error_UnexpectedAttribute")
    (fun () -> Terrabuild.Configuration.FrontEnd.Project.parse content |> ignore) |> should (throwWithMessage "unexpected attribute 'tagada'") typeof<Errors.TerrabuildException>

[<Test>]
let unexpectedBlockIsError() =
    let content = File.ReadAllText("TestFiles/Error_UnexpectedBlock")
    (fun () -> Terrabuild.Configuration.FrontEnd.Project.parse content |> ignore) |> should (throwWithMessage "unexpected block 'tagada'") typeof<Errors.TerrabuildException>

[<Test>]
let unexpectedNestedBlockIsError() =
    let content = File.ReadAllText("TestFiles/Error_UnexpectedNestedBlock")
    (fun () -> Terrabuild.Configuration.FrontEnd.Project.parse content |> ignore) |> should (throwWithMessage "unexpected nested block 'toto'") typeof<Errors.TerrabuildException>

[<Test>]
let duplicatedExtensionIsError() =
    let content = File.ReadAllText("TestFiles/Error_DuplicatedExtension")
    (fun () -> Terrabuild.Configuration.FrontEnd.Project.parse content |> ignore) |> should (throwWithMessage "duplicated extension '@dotnet'") typeof<Errors.TerrabuildException>

[<Test>]
let duplicatedTargetIsError() =
    let content = File.ReadAllText("TestFiles/Error_Project_DuplicatedTarget")
    (fun () -> Terrabuild.Configuration.FrontEnd.Project.parse content |> ignore) |> should (throwWithMessage "duplicated target 'build'") typeof<Errors.TerrabuildException>

[<Test>]
let duplicatedLocalIsError() =
    let content = File.ReadAllText("TestFiles/Error_DuplicatedLocal")
    (fun () -> Terrabuild.Configuration.FrontEnd.Project.parse content |> ignore) |> should (throwWithMessage "duplicated local 'app_name'") typeof<Errors.TerrabuildException>

[<Test>]
let customExtensionWithoutScriptIsError() =
    let content =
        """
project {}
extension dummy {}
"""
    (fun () -> Terrabuild.Configuration.FrontEnd.Project.parse content |> ignore)
    |> should (throwWithMessage "extension 'dummy' must declare 'script'") typeof<Errors.TerrabuildException>

[<Test>]
let projectOutputsSupportOrderedOperations() =
    let content =
        """
project {
  outputs += [ "dist/**" ]
  outputs -= [ "obj/**" ]
  outputs = [ "bin/**" ]
}
"""

    let project = Terrabuild.Configuration.FrontEnd.Project.parse content

    project.Project.Outputs
    |> should equal [
        outputAdd (Expr.List [ Expr.String "dist/**" ])
        outputRemove (Expr.List [ Expr.String "obj/**" ])
        outputAssign (Expr.List [ Expr.String "bin/**" ])
    ]

[<Test>]
let projectTargetDependsOnSupportsOrderedOperations() =
    let content =
        """
project {}

target build {
  depends_on += [ target.gen ]
  depends_on -= [ target.clean ]
  depends_on = [ target.dist ]
}
"""

    let project = Terrabuild.Configuration.FrontEnd.Project.parse content

    project.Targets["build"].DependsOn
    |> should equal [
        dependencyAdd (Set [ "gen" ])
        dependencyRemove (Set [ "clean" ])
        dependencyAssign (Set [ "dist" ])
    ]

[<Test>]
let outputsOperatorsAreRejectedForNonOutputsProjectAttributes() =
    let content =
        """
project {
  includes += [ "**/*" ]
}
"""

    (fun () -> Terrabuild.Configuration.FrontEnd.Project.parse content |> ignore)
    |> should (throwWithMessage "attribute 'includes' does not support operator 'Add'") typeof<Errors.TerrabuildException>

[<Test>]
let dependsOnOperatorsAreRejectedForProjectBlockAttributes() =
    let content =
        """
project {
  depends_on += [ project.lib ]
}
"""

    (fun () -> Terrabuild.Configuration.FrontEnd.Project.parse content |> ignore)
    |> should (throwWithMessage "attribute 'depends_on' does not support operator 'Add'") typeof<Errors.TerrabuildException>
