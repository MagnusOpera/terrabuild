module Terrabuild.Extensions.Tests.Npx

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers


[<Test>]
let ``run some``() =
    let expected =
        execRequest Cacheability.Local
                    [ shellOp("npx", "--yes \"--opt1\" \"--opt2\"") ]

    Npx.run someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``run none``() =
    let expected =
        execRequest Cacheability.Local
                    [ shellOp("npx", "--yes") ]

    Npx.run noneArgs
    |> normalize
    |> should equal expected
