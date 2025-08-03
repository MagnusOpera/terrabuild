module Terrabuild.Extensions.Tests.Gradle

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers


[<Test>]
let ``dispatch some``() =
    let expected =
        execRequest Cacheability.Never
                    [ shellOp("gradle", "ci-command --opt1 --opt2") ]

    Gradle.__dispatch__ ciContext someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``dispatch none``() =
    let expected =
        execRequest Cacheability.Never
                    [ shellOp("gradle", "local-command") ]

    Gradle.__dispatch__ localContext noneArgs
    |> normalize
    |> should equal expected



[<Test>]
let ``build some``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("gradle", "assemble dev --opt1 --opt2") ]

    Gradle.build (Some "dev") // configuration
                 someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``build none``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("gradle", "assemble dev") ]

    Gradle.build (Some "dev") // configuration
                 noneArgs
    |> normalize
    |> should equal expected
