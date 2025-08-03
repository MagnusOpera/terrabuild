module Terrabuild.Extensions.Tests.Helpers.Cargo

open NUnit.Framework
open FsUnit


[<Test>]
let ``find project file``() =
    CargoHelpers.findProjectFile "TestFiles/cargo-app"
    |> should equal "TestFiles/cargo-app/Cargo.toml"

[<Test>]
let ``find dependencies``() =
    // TODO: fix implementation
    CargoHelpers.findDependencies "TestFiles/cargo-app/Cargo.toml"
    |> should be Empty

