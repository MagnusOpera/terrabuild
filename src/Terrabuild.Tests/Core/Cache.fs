module Terrabuild.Tests.Core.Cache
open System
open System.IO
open FsUnit
open NUnit.Framework
open Collections

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

let private withHomeDir root action =
    let previousHome = Environment.GetEnvironmentVariable("HOME")
    Environment.SetEnvironmentVariable("HOME", root)
    try
        action ()
    finally
        Environment.SetEnvironmentVariable("HOME", previousHome)

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

let private createLocalCacheEntry (root: string) (id: string) entrySummary =
    let entryDir = Path.Combine(root, ".terrabuild", "cache", id.Replace('/', Path.DirectorySeparatorChar))
    let logsDir = Path.Combine(entryDir, "logs")
    Directory.CreateDirectory(logsDir) |> ignore
    File.WriteAllText(Path.Combine(logsDir, "summary.json"), entrySummary |> Json.Serialize)
    File.WriteAllText(Path.Combine(entryDir, "origin"), Cache.Origin.Local |> Json.Serialize)
    entryDir

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

[<Test>]
let ``cache completion omits summary outputs marker when outputs are not materialized`` () =
    withTempDir (fun root ->
        let storage = FakeStorage()
        let entryDir = Path.Combine(root, "entry")
        let entry = Cache.NewEntry(entryDir, false, "project-hash/build/target-hash", storage, None) :> Cache.IEntry
        let outputsPath = entry.Outputs

        entry.Complete(summary (Some outputsPath)) |> ignore

        let writtenSummary =
            Path.Combine(entry.Logs, "summary.json")
            |> File.ReadAllText
            |> Json.Deserialize<Cache.TargetSummary>

        writtenSummary.Outputs |> should equal None)

[<Test>]
let ``prune cache deletes stale entries and preserves fresh siblings`` () =
    withTempDir (fun root ->
        let staleEntry = createLocalCacheEntry root "project-hash/build/stale-target" (summary None)
        let freshEntry = createLocalCacheEntry root "project-hash/build/fresh-target" (summary None)
        let malformedEntry =
            Path.Combine(root, ".terrabuild", "cache", "project-hash", "build", "malformed-target")
        Directory.CreateDirectory(malformedEntry) |> ignore

        File.SetLastWriteTimeUtc(Path.Combine(staleEntry, "origin"), DateTime.UtcNow.AddDays(-10.0))
        File.SetLastWriteTimeUtc(Path.Combine(freshEntry, "origin"), DateTime.UtcNow.AddDays(-2.0))

        let pruneSummary =
            Cache.pruneCacheEntries (Path.Combine(root, ".terrabuild", "cache")) (DateTime.UtcNow.AddDays(-7.0))

        pruneSummary.Scanned |> should equal 3
        pruneSummary.Pruned |> should equal 1
        pruneSummary.Skipped |> should equal 2
        Directory.Exists(staleEntry) |> should equal false
        Directory.Exists(freshEntry) |> should equal true
        Directory.Exists(malformedEntry) |> should equal true
        Directory.Exists(Path.Combine(root, ".terrabuild", "cache", "project-hash", "build")) |> should equal true)

[<Test>]
let ``prune cache skips entries without touching home or tmp`` () =
    withTempDir (fun root ->
        let cacheRoot = Path.Combine(root, ".terrabuild", "cache")
        let homeRoot = Path.Combine(root, ".terrabuild", "home")
        let tmpRoot = Path.Combine(root, ".terrabuild", "tmp")
        let staleEntry = createLocalCacheEntry root "project-hash/build/stale-target" (summary None)

        Directory.CreateDirectory(homeRoot) |> ignore
        Directory.CreateDirectory(tmpRoot) |> ignore
        File.WriteAllText(Path.Combine(homeRoot, "keep.txt"), "home")
        File.WriteAllText(Path.Combine(tmpRoot, "keep.txt"), "tmp")
        File.SetLastWriteTimeUtc(Path.Combine(staleEntry, "origin"), DateTime.UtcNow.AddDays(-10.0))

        Cache.pruneCacheEntries cacheRoot (DateTime.UtcNow.AddDays(-7.0)) |> ignore

        Directory.Exists(staleEntry) |> should equal false
        File.Exists(Path.Combine(homeRoot, "keep.txt")) |> should equal true
        File.Exists(Path.Combine(tmpRoot, "keep.txt")) |> should equal true)

[<Test>]
let ``try get summary only refreshes origin timestamp for local cache entries`` () =
    withTempDir (fun root ->
        withHomeDir root (fun () ->
            let entryDir = createLocalCacheEntry root "project-hash/build/target-hash" (summary None)
            let originFile = Path.Combine(entryDir, "origin")
            let oldTimestamp = DateTime.UtcNow.AddDays(-10.0)
            File.SetLastWriteTimeUtc(originFile, oldTimestamp)

            let cache = Cache.Cache(FakeStorage(), None) :> Cache.ICache
            let result = cache.TryGetSummaryOnly false "project-hash/build/target-hash"
            let refreshedTimestamp = File.GetLastWriteTimeUtc(originFile)

            result |> should not' (equal None)
            refreshedTimestamp |> should be (greaterThan oldTimestamp)
        )
    )

[<Test>]
let ``try get summary only does not refresh origin timestamp when summary load fails`` () =
    withTempDir (fun root ->
        withHomeDir root (fun () ->
            let entryDir = Path.Combine(root, ".terrabuild", "cache", "project-hash", "build", "target-hash")
            let logsDir = Path.Combine(entryDir, "logs")
            let originFile = Path.Combine(entryDir, "origin")

            Directory.CreateDirectory(logsDir) |> ignore
            File.WriteAllText(Path.Combine(logsDir, "summary.json"), "{ invalid json")
            File.WriteAllText(originFile, Cache.Origin.Local |> Json.Serialize)

            let oldTimestamp = DateTime.UtcNow.AddDays(-10.0)
            File.SetLastWriteTimeUtc(originFile, oldTimestamp)

            let cache = Cache.Cache(FakeStorage(), None) :> Cache.ICache
            let result = cache.TryGetSummaryOnly false "project-hash/build/target-hash"
            let refreshedTimestamp = File.GetLastWriteTimeUtc(originFile)

            result |> should equal None
            refreshedTimestamp |> should equal oldTimestamp
        )
    )
