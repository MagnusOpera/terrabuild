module Terrabuild.Tests.Scripts.Npm

open FsUnit
open NUnit.Framework
open Terrabuild.ScriptingContracts
open Terrabuild.Tests.Scripts.Helpers

[<Test>]
let ``npm defaults include local file dependencies`` () =
    let context = localContext "build" (fixtureDir "npm-app")
    let result = invokeDefaults "@npm" context

    result.Dependencies |> should equal (set [ "../npm-lib" ])
    result.Outputs |> should equal (set [ "dist/**" ])

[<Test>]
let ``npm install defaults to clean-install`` () =
    let context = localContext "install" (fixtureDir "npm-app")
    let result = invokeResult "@npm" "install" context Map.empty

    result.Operations |> normalizeOps |> should equal [ op "npm" "clean-install" 0 ]

[<Test>]
let ``npm install cacheability is local`` () =
    cacheability "@npm" "install" |> should equal (Some Cacheability.Local)
