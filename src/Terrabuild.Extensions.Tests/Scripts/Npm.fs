module Terrabuild.Tests.Scripts.Npm

open FsUnit
open FsUnitTyped
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
    cacheability "@npm" "install" |> shouldEqual (Some Cacheability.Local)

[<Test>]
let ``node installs return lock declarations from their existing operations`` () =
    let directory = System.IO.Path.Combine(fixtureDir "", $"provenance-node-{System.Guid.NewGuid():N}")
    System.IO.Directory.CreateDirectory directory |> ignore
    try
        for extension, lockFile, optionName, disabledValue in ["@npm", "package-lock.json", "clean", false; "@pnpm", "pnpm-lock.yaml", "frozen", false; "@yarn", "yarn.lock", "update", true] do
            let context = localContext "install" directory
            (invokeResult extension "install" context Map.empty).PreserveProvenance |> should equal Terrabuild.ScriptingContracts.ProvenanceEffect.Broken
            System.IO.File.WriteAllText(System.IO.Path.Combine(directory, lockFile), "lock contents")
            (invokeResult extension "install" context Map.empty).PreserveProvenance |> should equal Terrabuild.ScriptingContracts.ProvenanceEffect.Preserved
            (invokeResult extension "install" context (Map [optionName, bool disabledValue])).PreserveProvenance |> should equal Terrabuild.ScriptingContracts.ProvenanceEffect.Broken
            (invokeResult extension "install" context (Map ["args", str "--any-override"])).PreserveProvenance |> should equal ProvenanceEffect.Broken
            (invokeResult extension "build" context Map.empty).PreserveProvenance |> should equal ProvenanceEffect.Broken
            System.IO.File.Delete(System.IO.Path.Combine(directory, lockFile))
    finally
        System.IO.Directory.Delete(directory, true)
