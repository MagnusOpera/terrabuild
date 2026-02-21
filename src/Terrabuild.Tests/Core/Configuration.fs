module Terrabuild.Tests.Core.Configuration
open Collections
open FsUnit
open NUnit.Framework
open System
open System.IO
open Errors

[<Test>]
let ``Matcher``() =
    let scanFolder = Configuration.scanFolders "tests/simple" (Set ["**/node_modules"; "**/.nuxt"; "**/.vscode"])
    scanFolder "tests/simple/.vscode" |> should equal false
    scanFolder "tests/simple/node_modules" |> should equal false
    scanFolder "tests/simple/toto/node_modules" |> should equal false
    scanFolder "tests/simple/toto/.out" |> should equal true
    scanFolder "tests/simple/toto/tagada.txt" |> should equal true
    scanFolder "tests/simple/src" |> should equal true

[<Test>]
let ``Extension script path must stay inside workspace``() =
    let root = Path.Combine(Path.GetTempPath(), $"terrabuild-tests-{Guid.NewGuid():N}")
    let workspace = Path.Combine(root, "workspace")
    Directory.CreateDirectory(workspace) |> ignore

    try
        let loader = Extensions.lazyLoadScript workspace [ ".git" ] "@custom" (Some "../outside.fss")
        (fun () -> loader.Value |> ignore)
        |> should (throwWithMessage $"Script '../outside.fss' is outside workspace '{workspace}'") typeof<TerrabuildException>
    finally
        if Directory.Exists(root) then Directory.Delete(root, true)

[<Test>]
let ``HTTP extension script URL is rejected``() =
    let workspace = Path.GetTempPath()
    let loader = Extensions.lazyLoadScript workspace [ ".git" ] "@custom" (Some "http://example.com/extension.fss")
    (fun () -> loader.Value |> ignore)
    |> should (throwWithMessage "Only HTTPS script URLs are allowed for extension '@custom'") typeof<TerrabuildException>
