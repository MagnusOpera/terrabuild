module Terrabuild.Tests.Scripts.FScript

open FsUnit
open NUnit.Framework
open Terrabuild.ScriptingContracts
open Terrabuild.Tests.Scripts.Helpers

[<Test>]
let ``fscript defaults do not contribute discovery metadata`` () =
    let context = localContext "execute" (fixtureDir "")
    let result = invokeDefaults "@fscript" context

    result.Dependencies.Count |> should equal 0
    result.Outputs.Count |> should equal 0

[<Test>]
let ``fscript execute runs script with project root sandbox and forwarded args`` () =
    let context = localContext "execute" (fixtureDir "")
    let args =
        Map.ofList
            [ "script", str "scripts/write-version.fss"
              "args", list [ str "arg1"; str "arg 2" ] ]

    let result = invokeResult "@fscript" "execute" context args

    result.Operations
    |> normalizeOps
    |> should equal
        [ op "fscript" "--root . \"scripts/write-version.fss\" -- \"arg1\" \"arg 2\"" 0 ]

[<Test>]
let ``fscript execute cacheability is never`` () =
    cacheability "@fscript" "execute" |> should equal (Some Cacheability.Never)
