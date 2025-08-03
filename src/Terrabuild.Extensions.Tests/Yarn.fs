module Terrabuild.Extensions.Tests.Yarn

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``__dispatch__ cacheability``() =
    getCacheInfo<Yarn> "__dispatch__" |> should equal Cacheability.Never

[<Test>]
let ``__dispatch__ some``() =
    let expected =
        [ shellOp("yarn", "ci-command -- --opt1 --opt2") ]

    Yarn.__dispatch__ ciContext someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``__dispatch__ none``() =
    let expected =
        [ shellOp("yarn", "local-command --") ]

    Yarn.__dispatch__ localContext noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``install cacheability``() =
    getCacheInfo<Yarn> "install" |> should equal Cacheability.Local

[<Test>]
let ``install some``() =
    let expected =
        [ shellOp("yarn", "install --ignore-engines --opt1 --opt2") ]

    Yarn.install (Some true) // update
                 (Some true) // ignore-engines
                 someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``install none``() =
    let expected =
        [ shellOp("yarn", "install --frozen-lockfile") ]

    Yarn.install None // update
                 None // ignore-engines
                 noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``build cacheability``() =
    getCacheInfo<Yarn> "build" |> should equal Cacheability.Remote

[<Test>]
let ``build some``() =
    let expected =
        [ shellOp("yarn", "build -- --opt1 --opt2") ]

    Yarn.build someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``build none``() =
    let expected =
        [ shellOp("yarn", "build --") ]

    Yarn.build noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``test cacheability``() =
    getCacheInfo<Yarn> "test" |> should equal Cacheability.Remote


[<Test>]
let ``test some``() =
    let expected =
        [ shellOp("yarn", "test -- --opt1 --opt2") ]

    Yarn.test someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``test none``() =
    let expected =
        [ shellOp("yarn", "test --") ]

    Yarn.test noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``run cacheability``() =
    getCacheInfo<Yarn> "run" |> should equal Cacheability.Local

[<Test>]
let ``run some``() =
    let expected =
        [ shellOp("yarn", "my-command -- --opt1 --opt2") ]

    Yarn.run "my-command" // command
              someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``run none``() =
    let expected =
        [ shellOp("yarn", "my-command --") ]

    Yarn.run "my-command" // command
              noneArgs
    |> normalize
    |> should equal expected
