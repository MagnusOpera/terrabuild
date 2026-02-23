module Terrabuild.Tests.Scripts.Shell

open FsUnit
open NUnit.Framework
open Terrabuild.ScriptingContracts
open Terrabuild.Tests.Scripts.Helpers

[<Test>]
let ``shell dispatch runs action as command`` () =
    let context = localContext "echo" (fixtureDir "")
    let result = invokeResult "@shell" "echo" context (Map.ofList [ "args", str "hello" ])

    result.Operations |> should equal [ op "echo" "hello" 0 ]

[<Test>]
let ``shell dispatch cacheability is never`` () =
    cacheability "@shell" "echo" |> should equal (Some Cacheability.Never)
