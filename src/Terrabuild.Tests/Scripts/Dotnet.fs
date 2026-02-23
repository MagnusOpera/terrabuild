module Terrabuild.Tests.Scripts.Dotnet

open System.IO
open FsUnit
open NUnit.Framework
open Terrabuild.ScriptingContracts
open Terrabuild.Tests.Scripts.Helpers

[<Test>]
let ``dotnet defaults reads project references`` () =
    let context = localContext "build" (fixtureDir "dotnet-app")
    let result = invokeDefaults "@dotnet" context

    result.Dependencies |> should equal (set [ "../dotnet-lib" ])
    result.Outputs |> should equal (set [ "bin/"; "obj/"; "**/*.binlog" ])

[<Test>]
let ``dotnet build default is remote cache`` () =
    cacheability "@dotnet" "build" |> should equal (Some Cacheability.Remote)

[<Test>]
let ``dotnet restore is batchable`` () =
    let context = localContext "restore" (fixtureDir "dotnet-app")
    let result = invokeResult "@dotnet" "restore" context Map.empty

    result.Batchable |> should equal true

[<Test>]
let ``dotnet restore batch generates slnx command`` () =
    let root = fixtureDir ""
    let tempDir = Path.Combine(root, ".terrabuild")
    let context =
        batchContext "restore" root tempDir [ "TestFiles/Scripts/dotnet-app"; "TestFiles/Scripts/dotnet-lib" ]

    let result = invokeResult "@dotnet" "restore" context Map.empty

    result.Batchable |> should equal true
    result.Operations.Length |> should equal 1
    result.Operations.Head.Command |> should equal "dotnet"
    result.Operations.Head.Arguments |> should contain "restore \""
    result.Operations.Head.Arguments |> should contain "FEDCBA.slnx"
    result.Operations.Head.Arguments |> should contain "--locked-mode"

[<Test>]
let ``dotnet test without restore and build flags`` () =
    let context = localContext "test" (fixtureDir "dotnet-app")
    let args =
        Map.ofList
            [ "configuration", str "Debug"
              "restore", bool false
              "build", bool false ]

    let result = invokeResult "@dotnet" "test" context args

    result.Operations
    |> normalizeOps
    |> should equal [ op "dotnet" "test --no-restore --no-build --configuration Debug" 0 ]
