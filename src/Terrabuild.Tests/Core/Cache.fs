module Terrabuild.Tests.Core.Cache
open System
open System.IO
open System.Text.Json
open FsUnit
open NUnit.Framework
open Collections

type private FakeStorage() =
    let uploads = ResizeArray<string>()
    let blobs = Collections.Generic.Dictionary<string, byte[]>()
    let mutable downloads = 0

    member _.Uploads = uploads |> Seq.toList
    member _.Downloads = downloads

    interface Contracts.IStorage with
        member _.Exists id = blobs.ContainsKey(id)
        member _.TryDownload id =
            match blobs.TryGetValue(id) with
            | true, contents ->
                downloads <- downloads + 1
                let file = Path.GetTempFileName()
                File.WriteAllBytes(file, contents)
                Some file
            | _ -> None
        member _.Upload id sourceFile =
            uploads.Add(id)
            blobs[id] <- File.ReadAllBytes(sourceFile)
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
      Cache.TargetSummary.HasOutputs = outputsDir |> Option.isSome
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
        let sourceDir = Path.Combine(root, "source")
        Directory.CreateDirectory(sourceDir) |> ignore
        let artifact = Path.Combine(sourceDir, "artifact.txt")
        File.WriteAllText(artifact, "artifact")
        entry.StoreOutputs sourceDir [ artifact ] |> ignore

        let files = entry.Complete(summary (Some "outputs"))

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

        entry.Complete(summary (Some "outputs")) |> ignore

        use writtenSummary =
            Path.Combine(entryDir, "logs", "summary.json")
            |> File.ReadAllText
            |> JsonDocument.Parse

        writtenSummary.RootElement.EnumerateObject()
        |> Seq.exists (fun property -> property.Name = "outputs")
        |> should equal false)

[<Test>]
let ``remote summary downloads become durable local entries`` () =
    withTempDir (fun root ->
        withHomeDir root (fun () ->
            let storage = FakeStorage()
            let id = "project-hash/build/remote-summary"
            let cache = Cache.Cache(storage, None) :> Cache.ICache
            cache.GetEntry true id |> fun entry -> entry.Complete(summary None) |> ignore

            IO.deleteAny (Path.Combine(root, ".terrabuild", "cache"))

            let downloaded = Cache.Cache(storage, None) :> Cache.ICache
            downloaded.TryGetSummaryOnly true id |> should not' (equal None)
            storage.Downloads |> should equal 1

            let offline = Cache.Cache(storage, None) :> Cache.ICache
            offline.TryGetSummaryOnly false id |> should not' (equal None)
            storage.Downloads |> should equal 1
        ))

[<Test>]
let ``remote summary without downloaded outputs is not offline-restorable`` () =
    withTempDir (fun root ->
        withHomeDir root (fun () ->
            let storage = FakeStorage()
            let id = "project-hash/build/remote-outputs"
            let source = Path.Combine(root, "source")
            Directory.CreateDirectory(source) |> ignore
            let output = Path.Combine(source, "artifact.txt")
            File.WriteAllText(output, "artifact")

            let producer = Cache.Cache(storage, None) :> Cache.ICache
            let entry = producer.GetEntry true id
            entry.StoreOutputs source [ output ] |> ignore
            entry.Complete(summary (Some "outputs")) |> ignore
            IO.deleteAny (Path.Combine(root, ".terrabuild", "cache"))

            let consumer = Cache.Cache(storage, None) :> Cache.ICache
            let _, downloadedSummary = consumer.TryGetSummaryOnly true id |> Option.get

            consumer.CanRestore false id downloadedSummary |> should equal false
            consumer.CanRestore true id downloadedSummary |> should equal true
        ))

[<Test>]
let ``new entries publish atomically over completed entries`` () =
    withTempDir (fun root ->
        let storage = FakeStorage()
        let entryDir = createLocalCacheEntry root "project-hash/build/target-hash" (summary None)
        let oldSummary = Path.Combine(entryDir, "logs", "summary.json") |> File.ReadAllText
        let entry = Cache.NewEntry(entryDir, false, "project-hash/build/target-hash", storage, None) :> Cache.IEntry

        Path.Combine(entryDir, "logs", "summary.json") |> File.ReadAllText |> should equal oldSummary

        entry.Complete({ summary None with Target = "replacement" }) |> ignore

        use published =
            Path.Combine(entryDir, "logs", "summary.json")
            |> File.ReadAllText
            |> JsonDocument.Parse
        published.RootElement.GetProperty("target").GetString() |> should equal "replacement")

[<Test>]
let ``restore replaces the complete declared output set`` () =
    withTempDir (fun root ->
        withHomeDir root (fun () ->
            let project = Path.Combine(root, "project")
            let generated = Path.Combine(project, "generated")
            let source = Path.Combine(root, "source")
            Directory.CreateDirectory(generated) |> ignore
            Directory.CreateDirectory(Path.Combine(source, "generated")) |> ignore
            File.WriteAllText(Path.Combine(generated, "cached.txt"), "old")
            File.WriteAllText(Path.Combine(generated, "stale.txt"), "stale")
            File.WriteAllText(Path.Combine(source, "generated", "cached.txt"), "cached")
            File.WriteAllText(Path.Combine(source, "generated", "new.txt"), "new")

            let id = "project-hash/build/restore"
            let cache = Cache.Cache(FakeStorage(), None) :> Cache.ICache
            let entry = cache.GetEntry false id
            entry.StoreOutputs source (IO.enumerateFiles source) |> ignore
            entry.Complete(summary (Some "outputs")) |> ignore

            cache.Restore false id (Set [ "generated/**" ]) project |> should not' (equal None)

            File.ReadAllText(Path.Combine(generated, "cached.txt")) |> should equal "cached"
            File.ReadAllText(Path.Combine(generated, "new.txt")) |> should equal "new"
            File.Exists(Path.Combine(generated, "stale.txt")) |> should equal false
        ))

[<Test>]
let ``failed restore rolls the workspace back`` () =
    withTempDir (fun root ->
        withHomeDir root (fun () ->
            let project = Path.Combine(root, "project")
            let generated = Path.Combine(project, "generated")
            let source = Path.Combine(root, "source")
            Directory.CreateDirectory(generated) |> ignore
            Directory.CreateDirectory(Path.Combine(source, "generated")) |> ignore
            File.WriteAllText(Path.Combine(generated, "a.txt"), "original")
            Directory.CreateDirectory(Path.Combine(generated, "z-conflict")) |> ignore
            File.WriteAllText(Path.Combine(source, "generated", "a.txt"), "replacement")
            File.WriteAllText(Path.Combine(source, "generated", "z-conflict"), "cannot replace a directory")

            let id = "project-hash/build/rollback"
            let cache = Cache.Cache(FakeStorage(), None) :> Cache.ICache
            let entry = cache.GetEntry false id
            entry.StoreOutputs source (IO.enumerateFiles source) |> ignore
            entry.Complete(summary (Some "outputs")) |> ignore

            (fun () -> cache.Restore false id (Set [ "generated/**" ]) project |> ignore)
            |> should throw typeof<IOException>

            File.ReadAllText(Path.Combine(generated, "a.txt")) |> should equal "original"
            Directory.Exists(Path.Combine(generated, "z-conflict")) |> should equal true
            Directory.EnumerateDirectories(root, ".terrabuild-restore-*") |> Seq.isEmpty |> should equal true
        ))

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
