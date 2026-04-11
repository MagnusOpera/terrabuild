module Terrabuild.Tests.Scripts.Npx

open FsUnit
open NUnit.Framework
open Terrabuild.ScriptingContracts
open Terrabuild.Tests.Scripts.Helpers

[<Test>]
let ``npx run command`` () =
    let context = localContext "run" (fixtureDir "")
    let result = invokeResult "@npx" "run" context (Map.ofList [ "package", str "hello-world-npm"; "args", str "--help" ])

    result.Operations |> normalizeOps |> should equal [ op "npx" "--yes -- hello-world-npm --help" 0 ]

[<Test>]
let ``npx run cacheability is local`` () =
    cacheability "@npx" "run" |> should equal (Some Cacheability.Local)
