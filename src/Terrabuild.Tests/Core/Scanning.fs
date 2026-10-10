module Terrabuild.Tests.Core.Scanning

open System
open System.IO
open System.Collections.Concurrent
open System.Diagnostics
open System.Runtime.ExceptionServices
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.FileSystemGlobbing
open LibGit2Sharp
open NUnit.Framework

let private assertEqual name expected actual =
    if actual <> expected then Assert.Fail($"{name}: scanning results differ")

// Retain the v0.201.0 walkers as behavioral and performance oracles.
let private legacyFilter root (ignores: Set<string>) =
    let matcher = Matcher()
    matcher.AddInclude("**/*").AddExcludePatterns(ignores)
    fun dir ->
        let relativeDir = FS.relativePath root dir
        if lock matcher (fun () -> matcher.Match(relativeDir).HasMatches) then
            match FS.combinePath dir "WORKSPACE" with
            | FS.File _ -> false
            | _ -> true
        else false

let private legacyScan maxConcurrency root scanFolder loadProject =
    use directories = new BlockingCollection<string>(ConcurrentQueue<string>())
    let errors = ConcurrentQueue<ExceptionDispatchInfo>()
    let mutable pending = 1
    directories.Add(root)
    let enqueue dir =
        Interlocked.Increment(&pending) |> ignore
        directories.Add(dir)
    let scan () =
        for dir in directories.GetConsumingEnumerable() do
            try
                try
                    if errors.IsEmpty && (dir = root || scanFolder dir) then
                        match FS.combinePath dir "PROJECT" with
                        | FS.File _ -> loadProject dir
                        | _ -> for child in IO.enumerateDirs dir do enqueue child
                with error -> errors.Enqueue(ExceptionDispatchInfo.Capture error)
            finally
                if Interlocked.Decrement(&pending) = 0 then directories.CompleteAdding()
    Parallel.For(0, maxConcurrency, fun _ -> scan ()) |> ignore
    match errors.TryDequeue() with
    | true, error -> error.Throw()
    | _ -> ()

let private legacyFiles workspace project =
    use repo = new Repository(Repository.Discover workspace)
    let repoDir = repo.Info.WorkingDirectory
    let ignored path =
        let relative = FS.relativePath repoDir path
        let pathForGit =
            if Directory.Exists path then
                if relative.EndsWith "/" then relative else relative + "/"
            else relative
        repo.Ignore.IsPathIgnored pathForGit
    let files = ResizeArray<string>()
    let stack = System.Collections.Generic.Stack<string>()
    stack.Push(Path.GetFullPath(Path.Combine(workspace, project)))
    while stack.Count > 0 do
        let dir = stack.Pop()
        if Path.GetFileName(dir) <> ".git" && not (ignored dir) then
            for file in Directory.EnumerateFiles dir do
                if not (ignored file) then files.Add(Path.GetFullPath file)
            for child in Directory.EnumerateDirectories dir do stack.Push child
    List.ofSeq files

let private withDirectory run =
    let root = Path.Combine(Path.GetTempPath(), $"terrabuild-scanning-{Guid.NewGuid():N}")
    Directory.CreateDirectory(root) |> ignore
    try run root
    finally Directory.Delete(root, true)

let private write root path (contents: string) =
    let file = Path.Combine(root, path)
    match Path.GetDirectoryName file with
    | null -> failwith "Fixture path has no parent directory"
    | parent -> Directory.CreateDirectory(parent) |> ignore
    File.WriteAllText(file, contents)

let private discover scan filter root ignores concurrency =
    let found = ConcurrentBag<string>()
    scan concurrency root (filter root ignores) (fun dir -> found.Add(FS.relativePath root dir))
    found |> Set.ofSeq

[<Test>]
let ``Project discovery matches release ignore and boundary behavior`` () =
    withDirectory (fun root ->
        for path in [ "a/PROJECT"; "a/child/PROJECT"; "b/PROJECT";
                      "ignored/PROJECT"; "nested/ignored/PROJECT"; "UPPER/PROJECT";
                      "bin/PROJECT"; "nested/bin/PROJECT"; "workspace/WORKSPACE";
                      "workspace/PROJECT"; "workspace/child/PROJECT";
                      "markers/PROJECT/child/PROJECT"; "markers/WORKSPACE/child/PROJECT" ] do
            write root path ""
        for ignores in [ Set.empty; Configuration.default_ignores;
                         Set ["ignored"; "upper"]; Set ["**/ignored"; "nested/*"];
                         Set ["**/bin"; "UPPER/"]; Set ["ignored/**"; "**/child"] ] do
            for concurrency in [1; 4] do
                let expected = discover legacyScan legacyFilter root ignores concurrency
                let actual = discover Configuration.scanProjectDirectories Configuration.scanFolders root ignores concurrency
                assertEqual $"ignores={ignores}, concurrency={concurrency}" expected actual
        write root "PROJECT" ""
        let actual = discover Configuration.scanProjectDirectories Configuration.scanFolders root Set.empty 4
        assertEqual "root project" (Set ["."]) actual)

[<Test>]
let ``Project scan preserves failures and rejects invalid worker counts`` () =
    withDirectory (fun root ->
        write root "PROJECT" ""
        Assert.Throws<InvalidOperationException>(Action(fun () ->
            Configuration.scanProjectDirectories 4 root (fun _ -> true) (fun _ -> raise (InvalidOperationException("load failure"))))) |> ignore
        Assert.Throws<ArgumentException>(Action(fun () ->
            Configuration.scanProjectDirectories 0 root (fun _ -> true) ignore)) |> ignore)
    withDirectory (fun root ->
        Assert.Throws<DirectoryNotFoundException>(Action(fun () ->
            Configuration.scanProjectDirectories 4 (Path.Combine(root, "missing")) (fun _ -> true) ignore)) |> ignore)

[<Test>]
let ``Git visible file scan matches release with nested ignores and symlinks`` () =
    withDirectory (fun root ->
        Repository.Init root |> ignore
        write root ".gitignore" "ignored/\n*.tmp\n!keep.tmp\n"
        for path in ["app/PROJECT"; "app/source.fs"; "app/ignored/hide.fs";
                     "app/drop.tmp"; "app/keep.tmp"; "app/nested/.gitignore";
                     "app/nested/visible.fs"; "app/nested/generated/hide.fs";
                     "app/nested/.git/objects/hide.fs"; "other/data.fs"] do
            write root path "contents"
        write root "app/nested/.gitignore" "generated/\n"
        if not (OperatingSystem.IsWindows()) then
            File.CreateSymbolicLink(Path.Combine(root, "app/file-link.fs"), "source.fs") |> ignore
            File.CreateSymbolicLink(Path.Combine(root, "app/broken-link.fs"), "missing.fs") |> ignore
            Directory.CreateSymbolicLink(Path.Combine(root, "app/directory-link"), "../other") |> ignore
            Directory.CreateSymbolicLink(Path.Combine(root, "app/ignored-link"), "ignored") |> ignore
        for project in ["app"; "app/nested"; "."] do
            let expected = legacyFiles root project |> Set.ofList
            let actual = Git.enumeratedCommittedFiles root project |> Set.ofList
            assertEqual project expected actual)

[<Test; Explicit("Read-only scanning benchmark; set TERRABUILD_SCAN_BENCHMARK_WORKSPACE")>]
let BenchmarkScanningAgainstReleaseWalkers () =
    let root = Environment.GetEnvironmentVariable "TERRABUILD_SCAN_BENCHMARK_WORKSPACE" |> Option.ofObj |> Option.defaultValue ""
    if String.IsNullOrWhiteSpace root then Assert.Ignore("Set TERRABUILD_SCAN_BENCHMARK_WORKSPACE")
    let root = Path.GetFullPath root
    let parallelism = max 1 (Environment.ProcessorCount / 2)
    let ignores = Configuration.default_ignores
    let projects = discover legacyScan legacyFilter root ignores parallelism |> Set.toArray
    let benchmark name baseline optimized =
        let expected = baseline ()
        assertEqual name expected (optimized ())
        for _ in 1..4 do baseline () |> ignore; optimized () |> ignore
        let measure action =
            let before = GC.GetTotalAllocatedBytes(true)
            let timer = Stopwatch.StartNew()
            let actual = action ()
            timer.Stop()
            let allocated = GC.GetTotalAllocatedBytes(true) - before
            assertEqual name expected actual
            timer.Elapsed.TotalMilliseconds, allocated
        for sample in 1..9 do
            let first, second = if sample % 2 = 0 then optimized, baseline else baseline, optimized
            let a, b = measure first, measure second
            let old, current = if sample % 2 = 0 then b, a else a, b
            printfn "SCAN_BENCH %s sample=%d baselineMs=%.3f optimizedMs=%.3f baselineBytes=%d optimizedBytes=%d" name sample (fst old) (fst current) (snd old) (snd current)
    benchmark "discovery"
        (fun () -> discover legacyScan legacyFilter root ignores parallelism)
        (fun () -> discover Configuration.scanProjectDirectories Configuration.scanFolders root ignores parallelism)
    let enumerate walker =
        projects |> Array.Parallel.map (fun project -> project, walker root project |> Set.ofList)
    benchmark "git-files" (fun () -> enumerate legacyFiles) (fun () -> enumerate Git.enumeratedCommittedFiles)
    printfn "SCAN_BENCH projects=%d parallelism=%d" projects.Length parallelism
