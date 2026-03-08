module Terrabuild.Tests.Core.Cache
open System
open System.IO
open FsUnit
open NUnit.Framework

type private FakeStorage() =
    let uploads = ResizeArray<string>()

    member _.Uploads = uploads |> Seq.toList

    interface Contracts.IStorage with
        member _.Exists _id = false
        member _.TryDownload _id = None
        member _.Upload id _summaryFile = uploads.Add(id)
        member _.Name = "fake"

let private withTempDir action =
    let root = Path.Combine(Path.GetTempPath(), $"terrabuild-cache-tests-{Guid.NewGuid():N}")
    Directory.CreateDirectory(root) |> ignore
    try
        action root
    finally
        if Directory.Exists(root) then
            Directory.Delete(root, true)

let private summary outputsDir =
    { Cache.TargetSummary.Project = "."
      Cache.TargetSummary.Target = "build"
      Cache.TargetSummary.Operations = []
      Cache.TargetSummary.Outputs = outputsDir
      Cache.TargetSummary.IsSuccessful = true
      Cache.TargetSummary.StartedAt = DateTime.UtcNow.AddSeconds(-1.0)
      Cache.TargetSummary.EndedAt = DateTime.UtcNow
      Cache.TargetSummary.Duration = TimeSpan.FromSeconds(1.0)
      Cache.TargetSummary.Cache = GraphDef.ArtifactMode.Workspace }

[<Test>]
let ``cache completion returns logs when outputs do not exist`` () =
    withTempDir (fun root ->
        let storage = FakeStorage()
        let entryDir = Path.Combine(root, "entry")
        let entry = Cache.NewEntry(entryDir, true, "project-hash/build/target-hash", storage, None) :> Cache.IEntry

        let files = entry.Complete(summary None)

        files |> should equal [ "logs" ]
        storage.Uploads |> should equal [ "project-hash/build/target-hash/logs" ])

[<Test>]
let ``cache completion returns logical names and uploads full storage ids`` () =
    withTempDir (fun root ->
        let storage = FakeStorage()
        let entryDir = Path.Combine(root, "entry")
        let entry = Cache.NewEntry(entryDir, true, "project-hash/build/target-hash", storage, None) :> Cache.IEntry
        Directory.CreateDirectory(entry.Outputs) |> ignore
        File.WriteAllText(Path.Combine(entry.Outputs, "artifact.txt"), "artifact")

        let files = entry.Complete(summary (Some entry.Outputs))

        files |> should equal [ "outputs"; "logs" ]
        storage.Uploads |> should equal [
            "project-hash/build/target-hash/outputs"
            "project-hash/build/target-hash/logs"
        ])
