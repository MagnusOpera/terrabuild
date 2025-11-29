module Terrabuild.Extensions.Tests.Helpers.Npm

open NUnit.Framework
open FsUnit


[<Test>]
let ``find npm local dependencies``() =
    let packageFile = NpmHelpers.findProjectFile "TestFiles/npm-app"
    packageFile |> should equal "TestFiles/npm-app/package.json"

    let package = NpmHelpers.loadPackage packageFile
    package.Name |> should equal "npm-app"

    let dependencies = package |> NpmHelpers.findLocalPackages
    dependencies |> should equal (Set [ "../npm-lib" ])

[<Test>]
let ``find pnpm workspace dependencies``() =
    let packageFile = NpmHelpers.findProjectFile "TestFiles/npm-app"
    packageFile |> should equal "TestFiles/npm-app/package.json"

    let package = NpmHelpers.loadPackage packageFile
    package.Name |> should equal "npm-app"

    let dependencies = package |> NpmHelpers.findWorkspacePackages
    dependencies |> should equal (Set [ "@npm-lib" ])
