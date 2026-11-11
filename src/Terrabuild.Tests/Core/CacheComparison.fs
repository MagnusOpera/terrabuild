module Terrabuild.Tests.Core.CacheComparison

open System
open System.IO
open Argu
open CLI
open Errors
open FsUnit
open FsUnitTyped
open NUnit.Framework

let private fixture () =
    Path.Combine(TestContext.CurrentContext.TestDirectory, "TestFiles/cache-comparison-v6.json")
    |> global.CacheComparison.readReport

let private changeKey key (node: Diagnostics.NodeReport) =
    { node with Fingerprint = node.Fingerprint |> Option.map (fun value -> { value with CacheKey = key; TargetHash = key }) }

let private withTempDir action =
    let directory = Path.Combine(Path.GetTempPath(), $"terrabuild-compare-{Guid.NewGuid():N}")
    Directory.CreateDirectory(directory) |> ignore
    try action directory
    finally Directory.Delete(directory, true)

[<Test>]
let ``comparison identifies changed added and removed project files`` () =
    let before = fixture()
    let project =
        { before.Projects.Head with
            Files =
                [ { Diagnostics.FileFingerprint.Path = "app/input.txt"; Hash = "file-b" }
                  { Diagnostics.FileFingerprint.Path = "app/new.txt"; Hash = "new" } ] }
    let beforeProject = { before.Projects.Head with Files = before.Projects.Head.Files @ [ { Diagnostics.FileFingerprint.Path = "app/old.txt"; Hash = "old" } ] }
    let before = { before with Projects = [ beforeProject ] }
    let after = { before with Projects = [ project ]; Nodes = [ changeKey "new-key" before.Nodes.Head ] }
    let result = global.CacheComparison.compare before after
    result.Counts["changed"] |> should equal 1
    let node = result.Nodes.Head
    node.Reasons |> should contain "project-files-changed"
    node.Files |> List.map (fun file -> file.Name, file.Kind)
    |> should equal [ "app/input.txt", "modified"; "app/new.txt", "added"; "app/old.txt", "removed" ]

[<Test>]
let ``comparison distinguishes fingerprint changes from cache availability`` () =
    let before = fixture()
    let node = { before.Nodes.Head with Action = Some "exec"; ActionReason = Some "cache-miss" }
    let result = global.CacheComparison.compare before { before with Nodes = [ node ] }
    result.Nodes.Head.Kind |> should equal "unchanged"
    result.Nodes.Head.DecisionChanged |> should equal true
    result.Nodes.Head.Files |> should be Empty
    global.CacheComparison.render result |> should haveSubstring "fingerprint unchanged"

[<Test>]
let ``comparison reports upstream configuration and evaluated input changes without plaintext values`` () =
    let before = fixture()
    let fingerprint = { before.Nodes.Head.Fingerprint.Value with
                            CacheKey = "new-key"; DeclaredTargetHash = Some "new-declaration"
                            OperationHashes = [ "new-operation" ]
                            DependencyTargetHashes = [ { Diagnostics.DependencyFingerprint.Id = "lib:build"; Hash = "new-dependency" } ] }
    let operation : Diagnostics.ResolvedOperationReport =
        { MetaCommand = "@shell run"
          Command = "shell"
          ArgumentsHash = "args-hash"
          Container = Some "private-secret-sentinel"
          Platform = None
          PlatformSensitivity = "portable"
          Cpus = None
          ForwardedVariableNames = []
          ForwardedVariablesFingerprint = None
          InjectedEnvironment = [] }
    let node =
        { before.Nodes.Head with
            Fingerprint = Some fingerprint
            EvaluationInputs = [ { GraphDef.EvaluationInput.Name = "var.secret"; ValueHash = "value-hash" } ]
            ResolvedOperations = [ operation ] }
    let project = { before.Projects.Head with Dependencies = [ { Diagnostics.DependencyFingerprint.Id = "lib"; Hash = "new-project-dependency" } ] }
    let result = global.CacheComparison.compare before { before with Nodes = [ node ]; Projects = [ project ] }
    result.Nodes.Head.Reasons |> should contain "project-dependencies-changed"
    result.Nodes.Head.Reasons |> should contain "target-dependencies-changed"
    result.Nodes.Head.Reasons |> should contain "target-definition-changed"
    result.Nodes.Head.Reasons |> should contain "operations-changed"
    result.Nodes.Head.Reasons |> should contain "evaluated-inputs-changed"
    (global.CacheComparison.render result).Contains("private-secret-sentinel") |> should equal false
    (Json.Serialize result).Contains("private-secret-sentinel") |> should equal false

[<Test>]
let ``comparison handles node addition removal unresolved fingerprints and portable paths`` () =
    let before = fixture()
    let portable = { before.Nodes.Head with Id = "other-id"; ProjectDirectory = "app\\." }
    (global.CacheComparison.compare before { before with Nodes = [ portable ] }).Nodes.Head.Kind |> should equal "unchanged"
    let changed = { before.Nodes.Head with Id = "new-id"; ProjectDirectory = "other" }
    let result = global.CacheComparison.compare before { before with Nodes = [ changed ] }
    result.Counts["added"] |> should equal 1
    result.Counts["removed"] |> should equal 1
    let unresolved = { before.Nodes.Head with Fingerprint = None }
    (global.CacheComparison.compare before { before with Nodes = [ unresolved ] }).Nodes.Head.Kind |> should equal "unresolved"
    let unselected = { before.Nodes.Head with Selected = false }
    (global.CacheComparison.compare { before with Nodes = [ unselected ] } { before with Nodes = [ unselected ] }).Nodes |> should be Empty

[<Test>]
let ``comparison rejects unsupported schemas and duplicate target identities`` () =
    let before = fixture()
    Assert.Throws<TerrabuildException>(fun () -> global.CacheComparison.compare before { before with SchemaVersion = 999 } |> ignore) |> ignore
    Assert.Throws<TerrabuildException>(fun () -> global.CacheComparison.compare before { before with Nodes = before.Nodes @ before.Nodes } |> ignore) |> ignore
    withTempDir (fun directory ->
        let path = Path.Combine(directory, "bad.json")
        File.WriteAllText(path, "not-json private-secret-sentinel")
        let error =
            Assert.Throws<TerrabuildException>(fun () -> global.CacheComparison.readReport path |> ignore)
            |> Option.ofObj
            |> Option.defaultWith (fun () -> failwith "Expected invalid diagnostic report to fail")
        error.Message.Contains("private-secret-sentinel") |> should equal false)

[<Test>]
let ``offline comparison preserves debug reports and supports structured output without a workspace`` () =
    withTempDir (fun directory ->
        let report = fixture()
        let before = Path.Combine(directory, "before.json")
        let after = Path.Combine(directory, "terrabuild-debug.json")
        let output = Path.Combine(directory, "out/comparison.json")
        File.WriteAllText(before, Json.Serialize report)
        File.WriteAllText(after, Json.Serialize report)
        let parser = ArgumentParser.Create<CLI.TerrabuildArgs>(programName = "terrabuild")
        let args = parser.ParseCommandLine([| "compare"; "--before"; before; "--after"; after; "--out"; output; "--debug" |], raiseOnUsage = true)
        let original = File.ReadAllText(after)
        global.Program.processCommandLine parser args |> should equal 0
        File.ReadAllText(after) |> should equal original
        let result = File.ReadAllText(output) |> Json.Deserialize<global.CacheComparison.Comparison>
        result.Counts["unchanged"] |> should equal 1
        let overwrite = parser.ParseCommandLine([| "compare"; "--before"; before; "--after"; after; "--out"; after |], raiseOnUsage = true)
        Assert.Throws<TerrabuildException>(fun () -> global.Program.processCommandLine parser overwrite |> ignore) |> ignore)


[<Test>]
let ``missing nodes in partial reports are unresolved rather than removed targets`` () =
    let before = fixture()
    let after = { before with Nodes = []; Run = { before.Run with Completeness = "partial" } }
    let result = global.CacheComparison.compare before after
    result.Nodes.Head.Kind |> should equal "unresolved"
    result.Nodes.Head.Reasons |> should equal [ "incomplete-report" ]
    result.Nodes.Head.Files |> should be Empty


[<Test>]
let ``missing baseline nodes do not invent file additions in partial reports`` () =
    let after = fixture()
    let before = { after with Nodes = []; Run = { after.Run with Completeness = "partial" } }
    let result = global.CacheComparison.compare before after
    result.Nodes.Head.Kind |> should equal "unresolved"
    result.Nodes.Head.Files |> should be Empty
