module Terrabuild.Tests.Scripts.Cargo

open FsUnit
open NUnit.Framework
open Terrabuild.ScriptingContracts
open Terrabuild.Tests.Scripts.Helpers

[<Test>]
let ``cargo defaults outputs`` () =
    let context = localContext "build" (fixtureDir "cargo-app")
    let result = invokeDefaults "@cargo" context

    result.Outputs |> should equal (set [ "target/debug/"; "target/release/" ])

[<Test>]
let ``cargo dispatch forwards command and args`` () =
    let context = localContext "build" (fixtureDir "cargo-app")
    let result = invokeResult "@cargo" "build" context (Map.ofList [ "args", str "--release" ])

    result.Operations |> normalizeOps |> should equal [ op "cargo" "build --release" 0 ]

[<Test>]
let ``cargo build cacheability is remote`` () =
    cacheability "@cargo" "build" |> should equal (Some Cacheability.Remote)
