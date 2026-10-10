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
