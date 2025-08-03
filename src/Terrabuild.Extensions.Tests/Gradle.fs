module Terrabuild.Extensions.Tests.Gradle

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers


// ------------------------------------------------------------------------------------------------

[<Test>]
let ``__dispatch__ cacheability``() =
    getCacheInfo<Gradle> "__dispatch__" |> should equal Cacheability.Never

[<Test>]
let ``__dispatch__ some``() =
    let expected =
        [ shellOp("gradle", "ci-command --opt1 --opt2") ]

    Gradle.__dispatch__ ciContext someArgs
    |> normalize
    |> should equal expected

[<Test>]
let ``__dispatch__ none``() =
    let expected =
        [ shellOp("gradle", "local-command") ]

    Gradle.__dispatch__ localContext noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``build cacheability``() =
    getCacheInfo<Gradle> "build" |> should equal Cacheability.Remote

[<Test>]
let ``build some``() =
    let expected =
        [ shellOp("gradle", "assemble dev --opt1 --opt2") ]

    Gradle.build (Some "dev") // configuration
                 someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``build none``() =
    let expected =
        [ shellOp("gradle", "assemble dev") ]

    Gradle.build (Some "dev") // configuration
                 noneArgs
    |> normalize
    |> should equal expected
