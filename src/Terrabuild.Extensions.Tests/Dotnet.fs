module Terrabuild.Extensions.Tests.Dotnet

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``__dispatch__ cacheability``() =
    getCacheInfo<Dotnet> "__dispatch__" |> should equal Cacheability.Never

[<Test>]
let ``__dispatch__ some``() =
    let expected =
        [ shellOp("dotnet", "ci-command --opt1 --opt2") ]

    Dotnet.__dispatch__ ciContext someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``__dispatch__ none``() =
    let expected =
        [ shellOp("dotnet", "local-command") ]

    Dotnet.__dispatch__ localContext noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``tool__ cacheability``() =
    getCacheInfo<Dotnet> "tool" |> should equal Cacheability.Local

[<Test>]
let ``tool_some``() =
    let expected =
        [ shellOp("dotnet", "tool --opt1 --opt2") ]

    Dotnet.tool someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``tool_none``() =
    let expected =
        [ shellOp("dotnet", "tool") ]

    Dotnet.tool noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``restore cacheability``() =
    getCacheInfo<Dotnet> "restore" |> should equal Cacheability.Local

[<Test>]
let ``restore batchability``() =
    getBatchInfo<Dotnet> "restore" |> should equal true

[<Test>]
let ``restore some``() =
    let expected =
        [ shellOp("dotnet", "restore --force-evaluate --opt1 --opt2") ]

    Dotnet.restore (Some true) // dependencies
                   (Some true) // floating
                   (Some true) // evaluate
                   someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``restore none``() =
    let expected =
        [ shellOp("dotnet", "restore --no-dependencies --locked-mode") ]

    Dotnet.restore None // dependencies
                   None // floating
                   None // evaluate
                   noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``build cacheability``() =
    getCacheInfo<Dotnet> "build" |> should equal Cacheability.Remote

[<Test>]
let ``build batchability``() =
    getBatchInfo<Dotnet> "build" |> should equal true

[<Test>]
let ``build some``() =
    let expected =
        [ shellOp("dotnet", "build --configuration Release -bl -maxcpucount:9 -p:Version=1.2.3 --opt1 --opt2") ]

    Dotnet.build (Some "Release") // configuration
                 (Some 9) // parallel
                 (Some true) // log
                 (Some true) // restore
                 (Some "1.2.3") // version
                 (Some true) // dependencies
                 someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``build none``() =
    let expected =
        [ shellOp("dotnet", "build --no-restore --no-dependencies --configuration Debug") ]

    Dotnet.build None // configuration
                 None // parallel
                 None // log
                 None // restore
                 None // version
                 None // dependencies
                 noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``pack cacheability``() =
    getCacheInfo<Dotnet> "pack" |> should equal Cacheability.Remote

[<Test>]
let ``pack some``() =
    let expected =
        [ shellOp("dotnet", "pack --configuration Release /p:Version=1.2.3 /p:TargetsForTfmSpecificContentInPackage= --opt1 --opt2") ]

    Dotnet.pack (Some "Release") // configuration
                (Some "1.2.3") // version
                (Some true) // restore
                (Some true)// build
                someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``pack none``() =
    let expected =
        [ shellOp("dotnet", "pack --no-restore --no-build --configuration Debug /p:Version=0.0.0 /p:TargetsForTfmSpecificContentInPackage=") ]

    Dotnet.pack None // configuration
                None // version
                None // restore
                None // build
                noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``publish cacheability``() =
    getCacheInfo<Dotnet> "publish" |> should equal Cacheability.Remote

[<Test>]
let ``publish some``() =
    let expected =
        [ shellOp("dotnet", "publish --configuration Release -r linux-arm64 -p:PublishTrimmed=true --self-contained --opt1 --opt2") ]

    Dotnet.publish (Some "Release") // configuration
                   (Some true) // restore
                   (Some true) // build
                   (Some "linux-arm64") // runtime
                   (Some true) // trim
                   (Some true) // single
                   someArgs
    |> normalize
    |> should equal expected

[<Test>]
let ``publish none``() =
    let expected =
        [ shellOp("dotnet", "publish --no-restore --no-build --configuration Debug") ]

    Dotnet.publish None // configuration
                   None // restore
                   None // build
                   None // runtime
                   None // trim
                   None // single
                   noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``test cacheability``() =
    getCacheInfo<Dotnet> "test" |> should equal Cacheability.Remote

[<Test>]
let ``test some``() =
    let expected =
        [ shellOp("dotnet", "test --configuration Release --filter \"TestCategory!=integration\" --opt1 --opt2") ]

    Dotnet.test (Some "Release") // configuration
                (Some true) // restore
                (Some true) // build
                (Some "TestCategory!=integration") // filter
                someArgs
    |> normalize
    |> should equal expected

[<Test>]
let ``test none``() =
    let expected =
        [ shellOp("dotnet", "test --no-restore --no-build --configuration Debug") ]

    Dotnet.test None // configuration
                None // restore
                None // build
                None // filter
                noneArgs
    |> normalize
    |> should equal expected
