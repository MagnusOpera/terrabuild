module Terrabuild.Tests.Scripts.Gradle

open FsUnit
open NUnit.Framework
open Terrabuild.ScriptingContracts
open Terrabuild.Tests.Scripts.Helpers

[<Test>]
let ``gradle build command`` () =
    let context = localContext "build" (fixtureDir "")
    let result = invokeResult "@gradle" "build" context (Map.ofList [ "configuration", str "Debug" ])

    result.Operations |> normalizeOps |> should equal [ op "gradle" "assemble Debug" 0 ]

[<Test>]
let ``gradle build cacheability is remote`` () =
    cacheability "@gradle" "build" |> should equal (Some Cacheability.Remote)
