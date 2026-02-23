module Terrabuild.Tests.Scripts.Make

open FsUnit
open NUnit.Framework
open Terrabuild.ScriptingContracts
open Terrabuild.Tests.Scripts.Helpers

[<Test>]
let ``make dispatch supports variables`` () =
    let context = localContext "build" (fixtureDir "")
    let args =
        Map.ofList
            [ "variables", map [ "configuration", str "Release"; "secret", str "tagada" ]
              "args", str "-d" ]

    let result = invokeResult "@make" "build" context args

    result.Operations |> normalizeOps |> should equal [ op "make" "build configuration=\"Release\" secret=\"tagada\" -d" 0 ]

[<Test>]
let ``make dispatch cacheability is never`` () =
    cacheability "@make" "build" |> should equal (Some Cacheability.Never)
