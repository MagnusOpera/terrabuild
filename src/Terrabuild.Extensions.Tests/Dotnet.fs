module Terrabuild.Extensions.Tests.Dotnet

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers

[<Test>]
let ``dispatch some``() =
    let expected =
        execRequest Cacheability.Never
                    [ shellOp("dotnet", "ci-command \"--opt1\" \"--opt2\"") ]

    Dotnet.__dispatch__ ciContext someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``dispatch none``() =
    let expected =
        execRequest Cacheability.Never
                    [ shellOp("dotnet", "local-command") ]

    Dotnet.__dispatch__ localContext noneArgs
    |> normalize
    |> should equal expected



[<Test>]
let ``restore some``() =
    let expected =
        execRequest Cacheability.Local
                    [ shellOp("dotnet", "restore --locked-mode \"--opt1\" \"--opt2\"") ]

    Dotnet.restore (Some true) // dependencies
                   (Some true) // locked
                   someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``restore none``() =
    let expected =
        execRequest Cacheability.Local
                    [ shellOp("dotnet", "restore --no-dependencies") ]

    Dotnet.restore None // dependencies
                   None // locked
                   noneArgs
    |> normalize
    |> should equal expected





[<Test>]
let ``build some``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("dotnet", "build --configuration Release -bl -maxcpucount:9 -p:Version=1.2.3 \"--opt1\" \"--opt2\"") ]

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
        execRequest Cacheability.Always
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



[<Test>]
let ``pack some``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("dotnet", "pack --configuration Release /p:Version=1.2.3 /p:TargetsForTfmSpecificContentInPackage= \"--opt1\" \"--opt2\"") ]

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
        execRequest Cacheability.Always
                    [ shellOp("dotnet", "pack --no-restore --no-build --configuration Debug /p:Version=0.0.0 /p:TargetsForTfmSpecificContentInPackage=") ]

    Dotnet.pack None // configuration
                None // version
                None // restore
                None // build
                noneArgs
    |> normalize
    |> should equal expected




[<Test>]
let ``publish some``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("dotnet", "publish --configuration Release -r linux-arm64 -p:PublishTrimmed=true --self-contained \"--opt1\" \"--opt2\"") ]

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
        execRequest Cacheability.Always
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



[<Test>]
let ``test some``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("dotnet", "test --configuration Release --filter \"TestCategory!=integration\" \"--opt1\" \"--opt2\"") ]

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
        execRequest Cacheability.Always
                    [ shellOp("dotnet", "test --no-restore --no-build --configuration Debug \"--opt1\" \"--opt2\"") ]

    Dotnet.test None // configuration
                None // restore
                None // build
                None // filter
                someArgs
    |> normalize
    |> should equal expected
