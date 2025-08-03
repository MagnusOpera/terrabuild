module Terrabuild.Extensions.Tests.Cargo

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers


// ------------------------------------------------------------------------------------------------

[<Test>]
let ``__dispatch__ cacheability``() =
    getCacheInfo<Cargo> "__dispatch__"
    |> should equal Cacheability.Never

[<Test>]
let ``__dispatch__ some``() =
    let expected =
        [ shellOp("cargo", "ci-command --opt1 --opt2") ]

    Cargo.__dispatch__ ciContext someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``__dispatch__ none``() =
    let expected =
        [ shellOp("cargo", "local-command") ]

    Cargo.__dispatch__ localContext noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``build cacheability``() =
    getCacheInfo<Cargo> "build"
    |> should equal Cacheability.Remote

[<Test>]
let ``build some``() =
    let expected =
        [ shellOp("cargo", "build --profile dev --opt1 --opt2") ]

    Cargo.build (Some "dev") // profile
                someArgs
    |> normalize
    |> should equal expected

[<Test>]
let ``build none``() =
    let expected = 
        [ shellOp("cargo", "build") ]

    Cargo.build None // profile
                noneArgs
    |> normalize
    |> should equal expected
