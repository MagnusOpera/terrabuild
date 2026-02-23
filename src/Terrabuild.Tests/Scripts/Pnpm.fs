module Terrabuild.Tests.Scripts.Pnpm

open FsUnit
open NUnit.Framework
open Terrabuild.ScriptingContracts
open Terrabuild.Tests.Scripts.Helpers

[<Test>]
let ``pnpm defaults expose package id and workspace dependencies`` () =
    let context = localContext "build" (fixtureDir "npm-app")
    let result = invokeDefaults "@pnpm" context

    result.Id |> should equal (Some "npm-app")
    result.Dependencies |> should equal (set [ "@npm-lib" ])

[<Test>]
let ``pnpm install batched uses recursive filters`` () =
    let root = fixtureDir ""
    let context =
        batchContext "install" root (fixtureDir ".terrabuild") [ "TestFiles/Scripts/npm-app"; "TestFiles/Scripts/npm-lib" ]

    let result = invokeResult "@pnpm" "install" context Map.empty

    result.Batchable |> should equal true
    result.Operations
    |> normalizeOps
    |> should equal [ op "pnpm" "--recursive --filter ./TestFiles/Scripts/npm-app --filter ./TestFiles/Scripts/npm-lib install --frozen-lockfile --link-workspace-packages --force" 0 ]

[<Test>]
let ``pnpm install cacheability is local`` () =
    cacheability "@pnpm" "install" |> should equal (Some Cacheability.Local)
