module Terrabuild.Extensions.Tests.Helpers.Dotnet

open NUnit.Framework
open FsUnit


[<Test>]
let ``find project file``() =
    DotnetHelpers.findProjectFile "TestFiles/dotnet-app"
    |> should equal "TestFiles/dotnet-app/dotnet-app.csproj"

[<Test>]
let ``find dependencies``() =
    DotnetHelpers.findDependencies "TestFiles/dotnet-app/dotnet-app.csproj"
    |> should equal (Set [ "../dotnet-lib" ])


