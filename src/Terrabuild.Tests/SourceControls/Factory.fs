module Terrabuild.Tests.FactorySourceControls

open System
open System.IO
open Errors
open FsUnit
open NUnit.Framework

let private withTempDirectory action =
    let root = Path.Combine(Path.GetTempPath(), $"terrabuild-source-control-factory-{Guid.NewGuid():N}")
    Directory.CreateDirectory(root) |> ignore

    try
        action root
    finally
        if Directory.Exists(root) then
            Directory.Delete(root, true)

[<Test>]
let ``factory rejects workspace outside git repository`` () =
    withTempDirectory (fun root ->
        let previousDir = Environment.CurrentDirectory
        Environment.CurrentDirectory <- root

        try
            try
                SourceControls.Factory.create() |> ignore
                Assert.Fail("Expected TerrabuildException")
            with
            | :? TerrabuildException as ex ->
                ex.Message |> should equal $"Current workspace '{Environment.CurrentDirectory}' is not a git repository"
        finally
            Environment.CurrentDirectory <- previousDir)
