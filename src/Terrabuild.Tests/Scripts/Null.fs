module Terrabuild.Tests.Scripts.Null

open FsUnit
open NUnit.Framework
open Terrabuild.ScriptingContracts
open Terrabuild.Tests.Scripts.Helpers

[<Test>]
let ``null fake returns no operations`` () =
    let context = localContext "fake" (fixtureDir "")
    let result = invokeResult "@null" "fake" context Map.empty

    result.Operations |> should be Empty

[<Test>]
let ``null fake cacheability is never`` () =
    cacheability "@null" "fake" |> should equal (Some Cacheability.Never)
