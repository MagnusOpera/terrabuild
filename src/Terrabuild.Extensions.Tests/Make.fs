module Terrabuild.Extensions.Tests.Make

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers


[<Test>]
let ``dispatch some``() =
    let expected =
        execRequest Cacheability.Never
                    [ shellOp("make", "ci-command arg1=\"value1\" arg2=\"value2\" --opt1 --opt2") ]

    Make.__dispatch__ ciContext
                     (["arg1", "value1"; "arg2", "value2"] |> Map |> Some) // variables
                      someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``dispatch none``() =
    let expected =
        execRequest Cacheability.Never
                    [ shellOp("make", "local-command") ]

    Make.__dispatch__ localContext
                      None // variables
                      noneArgs
    |> normalize
    |> should equal expected
