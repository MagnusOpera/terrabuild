module Terrabuild.Tests.Scripts.Go

open System
open FScript.Language
open FsUnit
open NUnit.Framework
open Terrabuild.ScriptingContracts
open Terrabuild.Tests.Scripts.Helpers

[<Test>]
let ``go defaults require module metadata and discover local replacements`` () =
    let context = localContext "build" (fixtureDir "go-app")
    let result = invokeDefaults "@go" context

    result.Id |> should equal (Some "example.com/go-app")
    result.DependencyResolution |> should equal (Some DependencyResolution.Scope)
    result.Dependencies |> should equal (set [ "example.com/go-lib" ])
    result.Outputs |> should equal (set [ "bin/" ])

[<Test>]
let ``go defaults reject projects without go mod`` () =
    let context = localContext "build" (fixtureDir "cargo-app")
    Assert.Throws<EvalException>(Action(fun () -> invokeDefaults "@go" context |> ignore)) |> ignore

[<Test>]
let ``go build uses cacheable bin output`` () =
    let context = localContext "build" (fixtureDir "go-app")
    let result = invokeResult "@go" "build" context Map.empty

    result.Batchable |> should equal true
    result.Operations |> normalizeOps |> should equal [ op "go" "build -o \"bin/\" ./..." 0 ]
    cacheability "@go" "build" |> should equal (Some Cacheability.Remote)

[<Test>]
let ``go install and test forward packages and args`` () =
    let context = localContext "install" (fixtureDir "go-app")
    let installArgs =
        Map.ofList
            [ "packages", list [ str "golang.org/x/tools/cmd/stringer@latest" ]
              "args", str "-v" ]

    let install = invokeResult "@go" "install" context installArgs
    install.Batchable |> should equal false
    install.Operations
    |> normalizeOps
    |> should equal [ op "go" "install -v golang.org/x/tools/cmd/stringer@latest" 0 ]

    let testContext = localContext "test" (fixtureDir "go-app")
    let testArgs = Map.ofList [ "packages", list [ str "./internal/..." ]; "args", str "-race" ]
    let test = invokeResult "@go" "test" testContext testArgs
    test.Operations |> normalizeOps |> should equal [ op "go" "test -race ./internal/..." 0 ]
    cacheability "@go" "install" |> should equal (Some Cacheability.Local)
    cacheability "@go" "test" |> should equal (Some Cacheability.Remote)

[<Test>]
let ``go batch emits one operation per module`` () =
    let root = fixtureDir ""
    let context =
        batchContext "build" root (fixtureDir ".terrabuild") [ "TestFiles/Scripts/go-app"; "TestFiles/Scripts/go-lib" ]

    let result = invokeResult "@go" "build" context Map.empty

    result.Batchable |> should equal true
    result.Operations
    |> normalizeOps
    |> should equal
        [ op "go" "-C \"TestFiles/Scripts/go-app\" build -o \"bin/\" ./..." 0
          op "go" "-C \"TestFiles/Scripts/go-lib\" build -o \"bin/\" ./..." 0 ]

[<Test>]
let ``go dispatch forwards command and args without batching`` () =
    let context = localContext "version" (fixtureDir "go-app")
    let result = invokeResult "@go" "version" context (Map.ofList [ "args", str "-m" ])

    result.Batchable |> should equal false
    result.Operations |> normalizeOps |> should equal [ op "go" "version -m" 0 ]
    cacheability "@go" "version" |> should equal (Some Cacheability.Never)
