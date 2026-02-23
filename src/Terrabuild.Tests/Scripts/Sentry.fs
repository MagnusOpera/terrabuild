module Terrabuild.Tests.Scripts.Sentry

open FsUnit
open NUnit.Framework
open Terrabuild.ScriptingContracts
open Terrabuild.Tests.Scripts.Helpers

[<Test>]
let ``sentry sourcemaps local upload tolerates errors`` () =
    let context = localContext "sourcemaps" (fixtureDir "")
    let result = invokeResult "@sentry" "sourcemaps" context Map.empty

    result.Operations
    |> should equal
        [ op "sentry-cli" "sourcemaps inject dist" 0
          op "sentry-cli" "sourcemaps upload  dist" 1 ]

[<Test>]
let ``sentry sourcemaps cacheability is external`` () =
    cacheability "@sentry" "sourcemaps" |> should equal (Some Cacheability.External)
