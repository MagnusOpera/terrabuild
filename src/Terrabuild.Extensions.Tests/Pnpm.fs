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
let ``restore batchability``() =
    getBatchInfo<Pnpm> "install" |> should equal true

[<Test>]
let ``install some``() =
    let expected =
        [ shellOp("pnpm", "--recursive install --frozen-lockfile --force --opt1 --opt2") ]

    Pnpm.install localContext
                 (Some true) // force
                 someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``install none``() =
    let expected =
        [ shellOp("pnpm", "--recursive install --frozen-lockfile") ]

    Pnpm.install localContext
                 None
                 noneArgs
    |> normalize
    |> should equal expected

[<Test>]
let ``install batch``() =
    let tmpDir = "TestFiles"
    let projectDirs = [ "TestFiles/npm-app"; "TestFiles/npm-lib" ]
    let expected =
        [ shellOp("pnpm", "--recursive --filter TestFiles/npm-app --filter TestFiles/npm-lib install --frozen-lockfile") ]

    Pnpm.install (batchContext tmpDir projectDirs)
                 None
                 noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``build cacheability``() =
    getCacheInfo<Pnpm> "build" |> should equal Cacheability.Remote

[<Test>]
let ``build batchability``() =
    getBatchInfo<Pnpm> "build" |> should equal true

[<Test>]
let ``build some``() =
    let expected =
        [ shellOp("pnpm", "--recursive run build --opt1 --opt2") ]

    Pnpm.build localContext
               someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``build none``() =
    let expected =
        [ shellOp("pnpm", "--recursive run build") ]

    Pnpm.build localContext
               noneArgs
    |> normalize
    |> should equal expected

[<Test>]
let ``build batch``() =
    let tmpDir = "TestFiles"
    let projectDirs = [ "TestFiles/npm-app"; "TestFiles/npm-lib" ]
    let expected =
        [ shellOp("pnpm", "--recursive --filter TestFiles/npm-app --filter TestFiles/npm-lib run build") ]

    Pnpm.build (batchContext tmpDir projectDirs)
               noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``test cacheability``() =
    getCacheInfo<Pnpm> "test" |> should equal Cacheability.Remote

[<Test>]
let ``test batchability``() =
    getBatchInfo<Pnpm> "test" |> should equal true

[<Test>]
let ``test some``() =
    let expected =
        [ shellOp("pnpm", "--recursive run test --opt1 --opt2") ]

    Pnpm.test localContext
              someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``test none``() =
    let expected =
        [ shellOp("pnpm", "--recursive run test") ]

    Pnpm.test localContext
              noneArgs
    |> normalize
    |> should equal expected

[<Test>]
let ``test batch``() =
    let tmpDir = "TestFiles"
    let projectDirs = [ "TestFiles/npm-app"; "TestFiles/npm-lib" ]
    let expected =
        [ shellOp("pnpm", "--recursive --filter TestFiles/npm-app --filter TestFiles/npm-lib run test") ]

    Pnpm.test (batchContext tmpDir projectDirs)
              noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``run cacheability``() =
    getCacheInfo<Pnpm> "run" |> should equal Cacheability.Local

[<Test>]
let ``run some``() =
    let expected =
        [ shellOp("pnpm", "run my-command --opt1 --opt2") ]

    Pnpm.run "my-command" // command
             (Some true) // no_recursive
             someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``run none``() =
    let expected =
        [ shellOp("pnpm", "--recursive run my-command") ]

    Pnpm.run "my-command" // command
             None
             noneArgs
    |> normalize
    |> should equal expected

