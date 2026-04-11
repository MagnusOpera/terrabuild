module Terrabuild.Tests.Scripts.Playwright

open FsUnit
open NUnit.Framework
open Terrabuild.ScriptingContracts
open Terrabuild.Tests.Scripts.Helpers

[<Test>]
let ``playwright test with browser and project`` () =
    let context = localContext "test" (fixtureDir "")
    let result =
        invokeResult
            "@playwright"
            "test"
            context
            (Map.ofList [ "browser", str "webkit"; "project", str "ci"; "args", str "--grep @smoke" ])

    result.Operations
    |> normalizeOps
    |> should equal [ op "npx" "playwright test --browser webkit --project ci --grep @smoke" 0 ]

[<Test>]
let ``playwright test cacheability is remote`` () =
    cacheability "@playwright" "test" |> should equal (Some Cacheability.Remote)
