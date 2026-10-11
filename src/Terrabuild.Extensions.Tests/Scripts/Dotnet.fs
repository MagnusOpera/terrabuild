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
let ``dotnet restore supports no-dependencies flag`` () =
    let context = localContext "restore" (fixtureDir "dotnet-app")
    let args = Map.ofList [ "dependencies", bool false ]

    let result = invokeResult "@dotnet" "restore" context args

    result.Operations
    |> normalizeOps
    |> should equal [ op "dotnet" "restore --locked-mode --no-dependencies" 0 ]

[<Test>]
let ``dotnet build supports no-dependencies flag`` () =
    let context = localContext "build" (fixtureDir "dotnet-app")
    let args = Map.ofList [ "dependencies", bool false ]

    let result = invokeResult "@dotnet" "build" context args

    result.Operations
    |> normalizeOps
    |> should equal [ op "dotnet" "build --no-dependencies --configuration Debug" 0 ]

[<Test>]
let ``dotnet pack supports no restore build dependencies flags`` () =
    let context = localContext "pack" (fixtureDir "dotnet-app")
    let args =
        Map.ofList
            [ "configuration", str "Debug"
              "restore", bool false
              "build", bool false
              "dependencies", bool false ]

    let result = invokeResult "@dotnet" "pack" context args

    result.Operations
    |> normalizeOps
    |> should equal [ op "dotnet" "pack --no-restore --no-build --no-dependencies --configuration Debug" 0 ]

[<Test>]
let ``dotnet publish supports no restore dependencies flags`` () =
    let context = localContext "publish" (fixtureDir "dotnet-app")
    let args =
        Map.ofList
            [ "configuration", str "Debug"
              "restore", bool false
              "dependencies", bool false ]

    let result = invokeResult "@dotnet" "publish" context args

    result.Operations
    |> normalizeOps
    |> should equal [ op "dotnet" "publish --no-restore --no-dependencies --configuration Debug" 0 ]

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

[<Test>]
let ``dotnet restore returns preservation only for a local lock with locking enabled`` () =
    let directory = Path.Combine(fixtureDir "", $"provenance-dotnet-{System.Guid.NewGuid():N}")
    Directory.CreateDirectory directory |> ignore
    try
        let context = localContext "restore" directory
        (invokeResult "@dotnet" "restore" context Map.empty).PreserveProvenance |> should equal Terrabuild.ScriptingContracts.ProvenanceEffect.Broken
        File.WriteAllText(Path.Combine(directory, "packages.lock.json"), "{}")
        (invokeResult "@dotnet" "restore" context Map.empty).PreserveProvenance |> should equal Terrabuild.ScriptingContracts.ProvenanceEffect.Preserved
        (invokeResult "@dotnet" "restore" context (Map ["locked", bool false])).PreserveProvenance |> should equal Terrabuild.ScriptingContracts.ProvenanceEffect.Broken
        (invokeResult "@dotnet" "restore" context (Map ["evaluate", bool true])).PreserveProvenance |> should equal Terrabuild.ScriptingContracts.ProvenanceEffect.Broken
        (invokeResult "@dotnet" "restore" context (Map ["args", str "--any-override"])).PreserveProvenance |> should equal ProvenanceEffect.Broken
        (invokeResult "@dotnet" "build" context Map.empty).PreserveProvenance |> should equal ProvenanceEffect.Broken
    finally
        Directory.Delete(directory, true)

[<Test>]
let ``dotnet build preserves declared inputs only when implicit restore is disabled`` () =
    let context = localContext "build" (fixtureDir "")
    (invokeResult "@dotnet" "build" context (Map ["restore", bool false])).PreserveProvenance
    |> should equal ProvenanceEffect.Preserved
    (invokeResult "@dotnet" "build" context (Map ["restore", bool false; "args", str "--any-override"])).PreserveProvenance
    |> should equal ProvenanceEffect.Broken
