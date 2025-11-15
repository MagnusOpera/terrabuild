module Terrabuild.Extensions.Tests.Pnpm

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``__dispatch__ cacheability``() =
    getCacheInfo<Pnpm> "__dispatch__" |> should equal Cacheability.Never

[<Test>]
let ``__dispatch__ some``() =
    let expected =
        [ shellOp("pnpm", "ci-command --opt1 --opt2") ]

    Pnpm.__dispatch__ ciContext someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``__dispatch__ none``() =
    let expected =
        [ shellOp("pnpm", "local-command") ]

    Pnpm.__dispatch__ localContext noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``install cacheability``() =
    getCacheInfo<Pnpm> "install" |> should equal Cacheability.Local

[<Test>]
let ``install some``() =
    let expected =
        [ shellOp("pnpm", "ci --force --opt1 --opt2") ]

    Pnpm.install (Some true) // force
                someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``install none``() =
    let expected =
        [ shellOp("pnpm", "ci") ]

    Pnpm.install None
                noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``build cacheability``() =
    getCacheInfo<Pnpm> "build" |> should equal Cacheability.Remote

[<Test>]
let ``build some``() =
    let expected =
        [ shellOp("pnpm", "run build -- --opt1 --opt2") ]

    Pnpm.build someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``build none``() =
    let expected =
        [ shellOp("pnpm", "run build --") ]

    Pnpm.build noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``test cacheability``() =
    getCacheInfo<Pnpm> "test" |> should equal Cacheability.Remote


[<Test>]
let ``test some``() =
    let expected =
        [ shellOp("pnpm", "run test -- --opt1 --opt2") ]

    Pnpm.test someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``test none``() =
    let expected =
        [ shellOp("pnpm", "run test --") ]

    Pnpm.test noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``run cacheability``() =
    getCacheInfo<Pnpm> "run" |> should equal Cacheability.Local

[<Test>]
let ``run some``() =
    let expected =
        [ shellOp("pnpm", "run my-command -- --opt1 --opt2") ]

    Pnpm.run "my-command" // command
             someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``run none``() =
    let expected =
        [ shellOp("pnpm", "run my-command --") ]

    Pnpm.run "my-command" // command
             noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``exec cacheability``() =
    getCacheInfo<Pnpm> "exec" |> should equal Cacheability.Local

[<Test>]
let ``exec some``() =
    let expected =
        [ shellOp("pnpm", "exec -- my-package --opt1 --opt2") ]

    Pnpm.exec "my-package" // command
             someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``exec none``() =
    let expected =
        [ shellOp("pnpm", "exec -- my-package") ]

    Pnpm.exec "my-package" // command
             noneArgs
    |> normalize
    |> should equal expected
