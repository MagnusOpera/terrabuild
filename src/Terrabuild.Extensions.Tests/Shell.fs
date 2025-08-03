module Terrabuild.Extensions.Tests.Shell

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers


[<Test>]
let ``dispatch some``() =
    let expected =
        execRequest Cacheability.Never
                    [ shellOp("ci-command", "--opt1 --opt2") ]

    Shell.__dispatch__ ciContext someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``dispatch none``() =
    let expected =
        execRequest Cacheability.Never
                    [ shellOp("local-command", "") ]

    Shell.__dispatch__ localContext noneArgs
    |> normalize
    |> should equal expected
