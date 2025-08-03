module Terrabuild.Extensions.Tests.Helpers.Cargo

open NUnit.Framework
open FsUnit


[<Test>]
let ``find project file``() =
    NpmHelpers.findProjectFile "TestFiles/cargo-app"
    |> should equal "TestFiles/cargo-app/Cargo.toml"

[<Test>]
let ``find dependencies``() =
    NpmHelpers.findDependencies "TestFiles/cargo-app/Cargo.toml"
    |> should equal (Set [])

