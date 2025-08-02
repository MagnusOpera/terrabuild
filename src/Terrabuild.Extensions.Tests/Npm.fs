module Terrabuild.Extensions.Tests.Npm

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers

[<Test>]
let ``dispatch some``() =
    let expected =
        execRequest Cacheability.Never
                    [ shellOp("npm", "ci-command \"--opt1\" \"--opt2\"") ]

    Npm.__dispatch__ ciContext someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``dispatch none``() =
    let expected =
        execRequest Cacheability.Never
                    [ shellOp("npm", "local-command") ]

    Npm.__dispatch__ localContext noneArgs
    |> normalize
    |> should equal expected



[<Test>]
let ``install some``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("npm", "ci --force \"--opt1\" \"--opt2\"") ]

    Npm.install (Some true) // force
                someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``install none``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("npm", "ci") ]

    Npm.install None
                noneArgs
    |> normalize
    |> should equal expected




[<Test>]
let ``build some``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("npm", "run build -- \"--opt1\" \"--opt2\"") ]

    Npm.build someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``build none``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("npm", "run build --") ]

    Npm.build noneArgs
    |> normalize
    |> should equal expected



[<Test>]
let ``test some``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("npm", "run test -- \"--opt1\" \"--opt2\"") ]

    Npm.test someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``test none``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("npm", "run test --") ]

    Npm.test noneArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``run some``() =
    let expected =
        execRequest Cacheability.Local
                    [ shellOp("npm", "run my-command -- \"--opt1\" \"--opt2\"") ]

    Npm.run "my-command" // command
             someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``run none``() =
    let expected =
        execRequest Cacheability.Local
                    [ shellOp("npm", "run my-command --") ]

    Npm.run "my-command" // command
             noneArgs
    |> normalize
    |> should equal expected
