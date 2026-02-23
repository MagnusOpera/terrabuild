module Terrabuild.Tests.Scripts.Yarn

open FsUnit
open NUnit.Framework
open Terrabuild.ScriptingContracts
open Terrabuild.Tests.Scripts.Helpers

[<Test>]
let ``yarn defaults include local file dependencies`` () =
    let context = localContext "build" (fixtureDir "npm-app")
    let result = invokeDefaults "@yarn" context

    result.Dependencies |> should equal (set [ "../npm-lib" ])

[<Test>]
let ``yarn install with update false keeps frozen lockfile`` () =
    let context = localContext "install" (fixtureDir "npm-app")
    let result = invokeResult "@yarn" "install" context (Map.ofList [ "update", bool false ])

    result.Operations |> normalizeOps |> should equal [ op "yarn" "install --frozen-lockfile" 0 ]

[<Test>]
let ``yarn install cacheability is local`` () =
    cacheability "@yarn" "install" |> should equal (Some Cacheability.Local)
