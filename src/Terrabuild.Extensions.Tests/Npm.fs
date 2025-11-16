module Terrabuild.Extensions.Tests.Npm

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``__dispatch__ cacheability``() =
    getCacheInfo<Npm> "__dispatch__" |> should equal Cacheability.Never

[<Test>]
let ``__dispatch__ some``() =
    let expected =
        [ shellOp("npm", "ci-command --opt1 --opt2") ]

    Npm.__dispatch__ ciContext someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``__dispatch__ none``() =
    let expected =
        [ shellOp("npm", "local-command") ]

    Npm.__dispatch__ localContext noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``install cacheability``() =
    getCacheInfo<Npm> "install" |> should equal Cacheability.Local

[<Test>]
let ``install some``() =
    let expected =
        [ shellOp("npm", "clean-install --force --opt1 --opt2") ]

    Npm.install (Some true) // force
                (Some true) // clean
                someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``install none``() =
    let expected =
        [ shellOp("npm", "install") ]

    Npm.install None // force
                None // clean
                noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``build cacheability``() =
    getCacheInfo<Npm> "build" |> should equal Cacheability.Remote

[<Test>]
let ``build some``() =
    let expected =
        [ shellOp("npm", "run build -- --opt1 --opt2") ]

    Npm.build someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``build none``() =
    let expected =
        [ shellOp("npm", "run build --") ]

    Npm.build noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``test cacheability``() =
    getCacheInfo<Npm> "test" |> should equal Cacheability.Remote


[<Test>]
let ``test some``() =
    let expected =
        [ shellOp("npm", "run test -- --opt1 --opt2") ]

    Npm.test someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``test none``() =
    let expected =
        [ shellOp("npm", "run test --") ]

    Npm.test noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``run cacheability``() =
    getCacheInfo<Npm> "run" |> should equal Cacheability.Local

[<Test>]
let ``run some``() =
    let expected =
        [ shellOp("npm", "run my-command -- --opt1 --opt2") ]

    Npm.run "my-command" // command
             someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``run none``() =
    let expected =
        [ shellOp("npm", "run my-command --") ]

    Npm.run "my-command" // command
             noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``exec cacheability``() =
    getCacheInfo<Npm> "exec" |> should equal Cacheability.Local

[<Test>]
let ``exec some``() =
    let expected =
        [ shellOp("npm", "exec -- my-package --opt1 --opt2") ]

    Npm.exec "my-package" // command
             someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``exec none``() =
    let expected =
        [ shellOp("npm", "exec -- my-package") ]

    Npm.exec "my-package" // command
             noneArgs
    |> normalize
    |> should equal expected
