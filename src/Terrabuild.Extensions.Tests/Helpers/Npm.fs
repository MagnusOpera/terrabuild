module Terrabuild.Extensions.Tests.Helpers.Npm

open NUnit.Framework
open FsUnit


[<Test>]
let ``find project file``() =
    NpmHelpers.findProjectFile "TestFiles/npm-app"
    |> should equal "TestFiles/npm-app/package.json"

[<Test>]
let ``find dependencies``() =
    NpmHelpers.findDependencies "TestFiles/npm-app/package.json"
    |> should equal (Set [ "../npm-lib" ])


