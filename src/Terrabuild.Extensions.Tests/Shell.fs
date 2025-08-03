module Terrabuild.Extensions.Tests.Shell

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers


// ------------------------------------------------------------------------------------------------

[<Test>]
let ``__dispatch__ cacheability``() =
    getCacheInfo<Shell> "__dispatch__" |> should equal Cacheability.Never

[<Test>]
let ``__dispatch__ some``() =
    let expected =
        [ shellOp("ci-command", "--opt1 --opt2") ]

    Shell.__dispatch__ ciContext someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``__dispatch__ none``() =
    let expected =
        [ shellOp("local-command", "") ]

    Shell.__dispatch__ localContext noneArgs
    |> normalize
    |> should equal expected
