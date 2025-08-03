module Terrabuild.Extensions.Tests.Make

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers


// ------------------------------------------------------------------------------------------------

[<Test>]
let ``__dispatch__ cacheability``() =
    getCacheInfo<Make> "__dispatch__" |> should equal Cacheability.Never

[<Test>]
let ``__dispatch__ some``() =
    let expected =
        [ shellOp("make", "ci-command arg1=\"value1\" arg2=\"value2\" --opt1 --opt2") ]

    Make.__dispatch__ ciContext
                     (["arg1", "value1"; "arg2", "value2"] |> Map |> Some) // variables
                      someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``__dispatch__ none``() =
    let expected =
        [ shellOp("make", "local-command") ]

    Make.__dispatch__ localContext
                      None // variables
                      noneArgs
    |> normalize
    |> should equal expected
