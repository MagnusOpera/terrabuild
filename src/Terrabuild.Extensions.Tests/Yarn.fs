module Terrabuild.Extensions.Tests.Yarn

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers

[<Test>]
let ``dispatch some``() =
    let expected =
        execRequest Cacheability.Never
                    [ shellOp("yarn", "ci-command -- \"--opt1\" \"--opt2\"") ]

    Yarn.__dispatch__ ciContext someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``dispatch none``() =
    let expected =
        execRequest Cacheability.Never
                    [ shellOp("yarn", "local-command --") ]

    Yarn.__dispatch__ localContext noneArgs
    |> normalize
    |> should equal expected



[<Test>]
let ``install some``() =
    let expected =
        execRequest Cacheability.Local
                    [ shellOp("yarn", "install --ignore-engines \"--opt1\" \"--opt2\"") ]

    Yarn.install (Some true) // update
                 (Some true) // ignore-engines
                 someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``install none``() =
    let expected =
        execRequest Cacheability.Local
                    [ shellOp("yarn", "install --frozen-lockfile") ]

    Yarn.install None // update
                 None // ignore-engines
                 noneArgs
    |> normalize
    |> should equal expected




[<Test>]
let ``build some``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("yarn", "build -- \"--opt1\" \"--opt2\"") ]

    Yarn.build someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``build none``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("yarn", "build --") ]

    Yarn.build noneArgs
    |> normalize
    |> should equal expected



[<Test>]
let ``test some``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("yarn", "test -- \"--opt1\" \"--opt2\"") ]

    Yarn.test someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``test none``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("yarn", "test --") ]

    Yarn.test noneArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``run some``() =
    let expected =
        execRequest Cacheability.Local
                    [ shellOp("yarn", "my-command -- \"--opt1\" \"--opt2\"") ]

    Yarn.run "my-command" // command
              someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``run none``() =
    let expected =
        execRequest Cacheability.Local
                    [ shellOp("yarn", "my-command --") ]

    Yarn.run "my-command" // command
              noneArgs
    |> normalize
    |> should equal expected
