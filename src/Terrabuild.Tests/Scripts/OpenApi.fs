module Terrabuild.Tests.Scripts.OpenApi

open FsUnit
open NUnit.Framework
open Terrabuild.ScriptingContracts
open Terrabuild.Tests.Scripts.Helpers

[<Test>]
let ``openapi generate command`` () =
    let context = localContext "generate" (fixtureDir "")
    let args =
        Map.ofList
            [ "generator", str "typescript-axios"
              "input", str "src/api.json"
              "output", str "src/client"
              "args", str "--strict-spec true" ]

    let result = invokeResult "@openapi" "generate" context args

    result.Operations
    |> normalizeOps
    |> should equal [ op "docker-entrypoint.sh" "generate -i src/api.json -g typescript-axios -o src/client --strict-spec true" 0 ]

[<Test>]
let ``openapi generate cacheability is remote`` () =
    cacheability "@openapi" "generate" |> should equal (Some Cacheability.Remote)
