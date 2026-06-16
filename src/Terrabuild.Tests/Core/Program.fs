module Terrabuild.Tests.Core.Program
open System
open System.IO
open System.Text.Json
open Argu
open CLI
open FsUnit
open NUnit.Framework

let private buildNodeInfo project target status =
    { Runner.NodeInfo.Request = Runner.TaskRequest.Exec
      Runner.NodeInfo.Status = status
      Runner.NodeInfo.Project = project
      Runner.NodeInfo.Target = target
      Runner.NodeInfo.ProjectHash = $"project-{project}"
      Runner.NodeInfo.TargetHash = $"target-{target}" }

let private buildSummary isSuccess nodes =
    { Runner.Summary.Commit = "commit"
      Runner.Summary.BranchOrTag = "main"
      Runner.Summary.StartedAt = DateTime.Parse("2026-04-11T16:42:46.595161Z").ToUniversalTime()
      Runner.Summary.EndedAt = DateTime.Parse("2026-04-11T16:43:28.917020Z").ToUniversalTime()
      Runner.Summary.IsSuccess = isSuccess
      Runner.Summary.Targets = Set [ "build"; "test" ]
      Runner.Summary.Nodes = nodes |> Map.ofList }

let private withTempDir action =
    let root = Path.Combine(Path.GetTempPath(), $"terrabuild-program-tests-{Guid.NewGuid():N}")
    Directory.CreateDirectory(root) |> ignore
    try
        action root
    finally
        if Directory.Exists(root) then
            Directory.Delete(root, true)

[<Test>]
let ``CLI parses run-result argument`` () =
    let parser = ArgumentParser.Create<CLI.TerrabuildArgs>(programName = "terrabuild")
    let result = parser.ParseCommandLine([| "run"; "build"; "--run-result"; "out.json" |], raiseOnUsage = true)
    let runArgs = result.GetResult(TerrabuildArgs.Run)

    runArgs.GetResult(RunArgs.Run_Result) |> should equal "out.json"

[<Test>]
let ``buildRunResult produces minimal jq friendly structure`` () =
    let summary =
        buildSummary true
            [ "node-a", buildNodeInfo "." "build" (Runner.TaskStatus.Success DateTime.UtcNow)
              "node-b", buildNodeInfo "src/Terrabuild.Common" "build" (Runner.TaskStatus.Success DateTime.UtcNow)
              "node-c", buildNodeInfo "src/Terrabuild.Common.Tests" "test" (Runner.TaskStatus.Success DateTime.UtcNow) ]

    let runResult = global.Program.buildRunResult summary

    runResult.Status |> should equal "success"
    runResult.Targets |> should equal [ "build"; "test" ]
    runResult.Results[".build"] |> should equal "success"
    runResult.Results["src/Terrabuild.Common.build"] |> should equal "success"
    runResult.Results["src/Terrabuild.Common.Tests.test"] |> should equal "success"

[<Test>]
let ``buildRunResult collapses duplicate project target keys with failure dominance`` () =
    let summary =
        buildSummary false
            [ "node-a", buildNodeInfo "src/App" "build" (Runner.TaskStatus.Success DateTime.UtcNow)
              "node-b", buildNodeInfo "src/App" "build" (Runner.TaskStatus.Failure (DateTime.UtcNow, "boom"))
              "node-c", buildNodeInfo "src/Lib" "build" (Runner.TaskStatus.Success DateTime.UtcNow) ]

    let runResult = global.Program.buildRunResult summary

    runResult.Status |> should equal "failure"
    runResult.Results.Count |> should equal 2
    runResult.Results["src/App.build"] |> should equal "failure"
    runResult.Results["src/Lib.build"] |> should equal "success"

[<Test>]
let ``writeRunResultFile writes JSON for successful summary`` () =
    withTempDir (fun root ->
        let path = Path.Combine(root, "nested", "run-result.json")
        let summary =
            buildSummary true
                [ "node-a", buildNodeInfo "." "build" (Runner.TaskStatus.Success DateTime.UtcNow) ]

        global.Program.writeRunResultFile path summary

        File.Exists(path) |> should equal true

        use doc = JsonDocument.Parse(File.ReadAllText(path))
        let json = doc.RootElement
        json.GetProperty("status").GetString() |> should equal "success"
        json.GetProperty("targets").EnumerateArray() |> Seq.map (fun item -> item.GetString()) |> Seq.toList |> should equal [ "build"; "test" ]
        json.GetProperty("results").GetProperty(".build").GetString() |> should equal "success")

[<Test>]
let ``writeRunResultFile writes JSON for failed summary and optional writer skips None`` () =
    withTempDir (fun root ->
        let path = Path.Combine(root, "run-result.json")
        let missingPath = Path.Combine(root, "missing.json")
        let summary =
            buildSummary false
                [ "node-a", buildNodeInfo "." "build" (Runner.TaskStatus.Failure (DateTime.UtcNow, "failed")) ]

        global.Program.writeRunResultFile path summary
        global.Program.tryWriteRunResultFile None summary

        File.Exists(path) |> should equal true
        File.Exists(missingPath) |> should equal false

        use doc = JsonDocument.Parse(File.ReadAllText(path))
        let json = doc.RootElement
        json.GetProperty("status").GetString() |> should equal "failure"
        json.GetProperty("results").GetProperty(".build").GetString() |> should equal "failure")
