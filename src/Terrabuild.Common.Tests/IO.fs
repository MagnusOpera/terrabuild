module Terrabuild.Tests.IO
open FsUnit
open NUnit.Framework
open System.IO
open System
open System.Diagnostics

[<Test>]
let ``Detect new files``() =
    let before = "TestFiles" |> IO.createSnapshot ["**/*.txt"]
    File.WriteAllText("TestFiles/tutu.txt", "tutu")
    let after = "TestFiles" |> IO.createSnapshot ["**/*.txt"]

    let diff = after - before

    let expected = [ "TestFiles/tutu.txt" |> FS.fullPath ]

    diff
    |> should equal expected

let private withOutputDirectory action =
    let root = Path.Combine(Path.GetTempPath(), $"terrabuild-outputs-{Guid.NewGuid():N}")
    Directory.CreateDirectory(root) |> ignore
    try action root
    finally Directory.Delete(root, true)

[<Test>]
let ``Output enumeration matches snapshot keys and ordering`` () =
    withOutputDirectory (fun root ->
        let nested = Path.Combine(root, "nested")
        Directory.CreateDirectory(nested) |> ignore
        for name in [ "a.txt"; "A.txt"; "z.txt"; "\u00E9.txt"; ".hidden.txt"; "other.json" ] do
            File.WriteAllText(Path.Combine(root, name), name)
        File.WriteAllText(Path.Combine(nested, "child.txt"), "nested")
        File.WriteAllText(Path.Combine(nested, "other.json"), "nested")
        if not (OperatingSystem.IsWindows()) then
            File.CreateSymbolicLink(Path.Combine(root, "linked.txt"), Path.Combine(nested, "child.txt")) |> ignore
            File.CreateSymbolicLink(Path.Combine(root, "broken.txt"), Path.Combine(root, "missing.txt")) |> ignore
        for patterns in [ []; [ "**/*" ]; [ "**/*.txt"; "nested/**"; "a.txt" ]; [ "missing/**" ] ] do
            let expected = IO.createSnapshot patterns root - IO.Snapshot.Empty
            IO.enumerateOutputFiles patterns root |> should equal expected
        let missing = Path.Combine(root, "missing-directory")
        IO.enumerateOutputFiles [ "**/*" ] missing
        |> should equal (IO.createSnapshot [ "**/*" ] missing - IO.Snapshot.Empty))

[<Test; Explicit("Set TERRABUILD_OUTPUT_BENCHMARK_DIRECTORY for existing managed outputs, or use a synthetic tree")>]
let BenchmarkOutputEnumerationAgainstSnapshots () =
    let benchmark root patterns =
        let legacy () = IO.createSnapshot patterns root - IO.Snapshot.Empty
        let optimized () = IO.enumerateOutputFiles patterns root
        let measure action =
            let before = GC.GetTotalAllocatedBytes(true)
            let timer = Stopwatch.StartNew()
            let result = action ()
            timer.Stop()
            result, timer.Elapsed.TotalMilliseconds, GC.GetTotalAllocatedBytes(true) - before
        for _ in 1..4 do
            legacy () |> ignore
            optimized () |> ignore
        for sample in 1..11 do
            let before, after =
                if sample % 2 = 1 then measure legacy, measure optimized
                else
                    let after = measure optimized
                    let before = measure legacy
                    before, after
            let expected, baselineMs, baselineBytes = before
            let actual, optimizedMs, optimizedBytes = after
            actual |> should equal expected
            TestContext.Out.WriteLine(FormattableString.Invariant($"OUTPUT_BENCH sample={sample} files={actual.Length} baselineMs={baselineMs:F3} optimizedMs={optimizedMs:F3} baselineBytes={baselineBytes} optimizedBytes={optimizedBytes}"))
    match Environment.GetEnvironmentVariable("TERRABUILD_OUTPUT_BENCHMARK_DIRECTORY") with
    | null | "" ->
        withOutputDirectory (fun root ->
            for directory in 0..63 do
                let path = Path.Combine(root, $"directory-{directory}")
                Directory.CreateDirectory(path) |> ignore
                for file in 0..63 do
                    File.WriteAllText(Path.Combine(path, $"output-{file}.txt"), "output")
            benchmark root [ "**/*" ])
    | root ->
        let patterns =
            match Environment.GetEnvironmentVariable("TERRABUILD_OUTPUT_BENCHMARK_PATTERNS") with
            | null | "" -> [ "**/*" ]
            | value -> value.Split(';') |> Array.toList
        benchmark root patterns

[<Test>]
let ``copy keeps symbolic file links instead of dereferencing them`` () =
    if OperatingSystem.IsWindows() then Assert.Ignore("Requires POSIX symbolic-link creation")
    withOutputDirectory (fun root ->
        let source = Path.Combine(root, "source")
        let copied = Path.Combine(root, "copied")
        Directory.CreateDirectory(source) |> ignore
        File.WriteAllText(Path.Combine(source, "target.js"), "launcher")
        File.CreateSymbolicLink(Path.Combine(source, "tool"), "target.js") |> ignore
        File.CreateSymbolicLink(Path.Combine(source, "missing"), "not-created.js") |> ignore
        IO.copyFiles copied source [ Path.Combine(source, "target.js"); Path.Combine(source, "tool"); Path.Combine(source, "missing") ] |> ignore
        Assert.That(String.Equals(FileInfo(Path.Combine(copied, "tool")).LinkTarget, "target.js", StringComparison.Ordinal), Is.True)
        File.ReadAllText(Path.Combine(copied, "tool")) |> should equal "launcher"
        Assert.That(String.Equals(FileInfo(Path.Combine(copied, "missing")).LinkTarget, "not-created.js", StringComparison.Ordinal), Is.True))

[<Test>]
let ``copy replaces destination links without writing through them`` () =
    if OperatingSystem.IsWindows() then Assert.Ignore("Requires POSIX symbolic-link creation")
    withOutputDirectory (fun root ->
        let source = Path.Combine(root, "source")
        let copied = Path.Combine(root, "copied")
        Directory.CreateDirectory(source) |> ignore
        Directory.CreateDirectory(copied) |> ignore
        let outside = Path.Combine(root, "untouched.txt")
        File.WriteAllText(outside, "untouched")
        File.WriteAllText(Path.Combine(source, "output.txt"), "new output")
        File.CreateSymbolicLink(Path.Combine(copied, "output.txt"), outside) |> ignore
        IO.copyFiles copied source [ Path.Combine(source, "output.txt") ] |> ignore
        File.ReadAllText(outside) |> should equal "untouched"
        File.ReadAllText(Path.Combine(copied, "output.txt")) |> should equal "new output"
        Assert.That(isNull (FileInfo(Path.Combine(copied, "output.txt")).LinkTarget), Is.True))

[<TestCase("/outside")>]
[<TestCase("../../../outside")>]
let ``tar rejects symbolic links outside its extraction directory`` linkTarget =
    withOutputDirectory (fun root ->
        let archive = Path.Combine(root, "archive.tar")
        do
            use stream = File.Create(archive)
            use writer = new System.Formats.Tar.TarWriter(stream)
            let entry = System.Formats.Tar.PaxTarEntry(System.Formats.Tar.TarEntryType.SymbolicLink, "node_modules/.bin/tool")
            entry.LinkName <- linkTarget
            writer.WriteEntry(entry)
        (fun () -> Compression.untar (Path.Combine(root, "output")) archive)
        |> should throw typeof<ArgumentException>)

[<Test>]
let ``tar refuses to write through existing symbolic-link parents`` () =
    if OperatingSystem.IsWindows() then Assert.Ignore("Requires POSIX symbolic-link creation")
    withOutputDirectory (fun root ->
        let output = Path.Combine(root, "output")
        let outside = Path.Combine(root, "outside")
        Directory.CreateDirectory(output) |> ignore
        Directory.CreateDirectory(outside) |> ignore
        Directory.CreateSymbolicLink(Path.Combine(output, "linked"), outside) |> ignore
        let archive = Path.Combine(root, "archive.tar")
        do
            use stream = File.Create(archive)
            use writer = new System.Formats.Tar.TarWriter(stream)
            use content = new MemoryStream([| 42uy |])
            let entry = System.Formats.Tar.PaxTarEntry(System.Formats.Tar.TarEntryType.RegularFile, "linked/file.txt")
            entry.DataStream <- content
            writer.WriteEntry(entry)
        (fun () -> Compression.untar output archive) |> should throw typeof<ArgumentException>
        File.Exists(Path.Combine(outside, "file.txt")) |> should equal false)

[<Test>]
let ``tar round trip keeps relative symbolic file links`` () =
    if OperatingSystem.IsWindows() then Assert.Ignore("Requires POSIX symbolic-link creation")
    withOutputDirectory (fun root ->
        let source = Path.Combine(root, "source")
        let output = Path.Combine(root, "output")
        Directory.CreateDirectory(source) |> ignore
        File.WriteAllText(Path.Combine(source, "target.js"), "launcher")
        File.CreateSymbolicLink(Path.Combine(source, "tool"), "target.js") |> ignore
        let archive = Compression.tar source
        try
            Compression.untar (output + string Path.DirectorySeparatorChar) archive
            Assert.That(String.Equals(FileInfo(Path.Combine(output, "tool")).LinkTarget, "target.js", StringComparison.Ordinal), Is.True)
            File.ReadAllText(Path.Combine(output, "tool")) |> should equal "launcher"
        finally File.Delete(archive))

[<Test>]
let ``tar refuses directory aliases before cache publication`` () =
    withOutputDirectory (fun root ->
        let archive = Path.Combine(root, "archive.tar")
        do
            use stream = File.Create(archive)
            use writer = new System.Formats.Tar.TarWriter(stream)
            writer.WriteEntry(System.Formats.Tar.PaxTarEntry(System.Formats.Tar.TarEntryType.Directory, "package"))
            let entry = System.Formats.Tar.PaxTarEntry(System.Formats.Tar.TarEntryType.SymbolicLink, "alias")
            entry.LinkName <- "package"
            writer.WriteEntry(entry)
        (fun () -> Compression.untar (Path.Combine(root, "output")) archive)
        |> should throw typeof<ArgumentException>)
