module Terrabuild.Extensions.Tests.Cargo

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers


[<Test>]
let ``dispatch some``() =
    let expected =
        execRequest Cacheability.Never
                    [ shellOp("cargo", "ci-command \"--opt1\" \"--opt2\"") ]

    Cargo.__dispatch__ ciContext someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``dispatch none``() =
    let expected =
        execRequest Cacheability.Never
                    [ shellOp("cargo", "local-command") ]

    Cargo.__dispatch__ localContext noneArgs
    |> normalize
    |> should equal expected



[<Test>]
let ``build some``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("cargo", "build --profile dev \"--opt1\" \"--opt2\"") ]

    Cargo.build (Some "dev") // profile
                someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``build none``() =
    let expected = 
        execRequest Cacheability.Always
                    [ shellOp("cargo", "build") ]

    Cargo.build None // profile
                noneArgs
    |> normalize
    |> should equal expected
