module Terrabuild.Extensions.Tests.Cargo

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers

[<Test>]
let ``build some``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("cargo", "build --profile dev \"--opt1\" \"--opt2\"") ]

    Cargo.build (Some "dev") someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``build none``() =
    let expected = 
        execRequest Cacheability.Always
                    [ shellOp("cargo", "build") ]

    Cargo.build None noneArgs
    |> normalize
    |> should equal expected
