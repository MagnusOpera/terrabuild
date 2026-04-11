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
    result.DependencyResolution |> should equal (Some DependencyResolution.Scope)
    result.Dependencies |> should equal (set [ "@npm-lib" ])

[<Test>]
let ``pnpm defaults keep scoped id when dependencies are missing`` () =
    let context = localContext "build" (fixtureDir "npm-lib")
    let result = invokeDefaults "@pnpm" context

    result.Id |> should equal (Some "npm-lib")
    result.DependencyResolution |> should equal (Some DependencyResolution.Scope)
    result.Dependencies.Count |> should equal 0

[<Test>]
let ``pnpm defaults keep scoped id when dependencies are empty`` () =
    let context = localContext "build" (fixtureDir "npm-emptydeps")
    let result = invokeDefaults "@pnpm" context

    result.Id |> should equal (Some "npm-emptydeps")
    result.DependencyResolution |> should equal (Some DependencyResolution.Scope)
    result.Dependencies.Count |> should equal 0

[<Test>]
let ``pnpm defaults keep package scope in generated id`` () =
    let context = localContext "build" (fixtureDir "npm-scoped")
    let result = invokeDefaults "@pnpm" context

    result.Id |> should equal (Some "@matis/investapi")
    result.DependencyResolution |> should equal (Some DependencyResolution.Scope)
    result.Dependencies.Count |> should equal 0

[<Test>]
let ``pnpm install batched uses recursive filters`` () =
    let root = fixtureDir ""
    let context =
        batchContext "install" root (fixtureDir ".terrabuild") [ "TestFiles/Scripts/npm-app"; "TestFiles/Scripts/npm-lib" ]

    let result = invokeResult "@pnpm" "install" context Map.empty

    result.Batchable |> should equal true
    result.Operations
    |> normalizeOps
    |> should equal [ op "pnpm" "--recursive --filter ./TestFiles/Scripts/npm-app --filter ./TestFiles/Scripts/npm-lib install --frozen-lockfile --link-workspace-packages" 0 ]

[<Test>]
let ``pnpm install force=true adds force flag`` () =
    let context = localContext "install" (fixtureDir "npm-app")
    let result = invokeResult "@pnpm" "install" context (Map.ofList [ "force", bool true ])

    result.Operations
    |> normalizeOps
    |> should equal [ op "pnpm" "install --frozen-lockfile --link-workspace-packages --force" 0 ]

[<Test>]
let ``pnpm install cacheability is local`` () =
    cacheability "@pnpm" "install" |> should equal (Some Cacheability.Local)
