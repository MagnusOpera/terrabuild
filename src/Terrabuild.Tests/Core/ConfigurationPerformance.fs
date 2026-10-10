module Terrabuild.Tests.Core.ConfigurationPerformance

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.IO
open FSharp.Reflection
open NUnit.Framework
open Terrabuild.Expression

// Retain the original walk as an independent parity and performance oracle.
let private legacyReflection collect (value: obj) =
    let rec walk (value: obj | null) =
        seq {
            match value with
            | :? IEnumerable as values ->
                for value in values do yield! walk value
            | :? Expr as expr -> yield collect expr
            | NonNull value ->
                let ty = value.GetType()
                if ty.IsGenericType then
                    let definition = ty.GetGenericTypeDefinition()
                    let property =
                        if definition = typedefof<Map<string, _>> then ty.GetProperty("Values")
                        elif definition = typedefof<option<_>> || definition = typedefof<KeyValuePair<string, _>> then ty.GetProperty("Value")
                        else null
                    match property with
                    | NonNull property -> yield! walk (property.GetValue(value, null))
                    | _ -> ()
                elif FSharpType.IsRecord(ty, false) then
                    for property in FSharpType.GetRecordFields ty do
                        yield! walk (property.GetValue(value, null))
            | _ -> ()
        }
    walk value |> Seq.fold Set.union Set.empty

let private legacyDependencies value =
    legacyReflection Dependencies.find value,
    legacyReflection Dependencies.findProjectReferences value

[<Test>]
let ``Combined dependency scan matches legacy parsed configurations`` () =
    let configs =
        [ "project { } target build { @shell echo { args = \"${project.library.version}\" } }"
          "project { depends_on = [project.library] } locals { name = \"${project.library.version}\" } target build { @shell echo { args = local.name } }"
          "project { } extension @custom { defaults { args = \"${project.library.version}\" } } target build { @custom run { } }" ]
    for content in configs do
        let config = Terrabuild.Configuration.FrontEnd.Project.parseWithSource "PROJECT" content
        Assert.That(Dependencies.reflectionFindAll config, Is.EqualTo(legacyDependencies config))

[<Test; Explicit("Configuration component benchmark")>]
let BenchmarkConfigurationDependencies () =
    let root = Environment.GetEnvironmentVariable "TERRABUILD_SCAN_BENCHMARK_WORKSPACE" |> Option.ofObj |> Option.defaultValue ""
    if String.IsNullOrWhiteSpace root then Assert.Ignore("Set TERRABUILD_SCAN_BENCHMARK_WORKSPACE")
    let projects = ResizeArray<string>()
    Configuration.scanProjectDirectories 1 root (Configuration.scanFolders root Configuration.default_ignores) projects.Add
    let configs =
        projects
        |> Seq.sort
        |> Seq.map (fun dir ->
            let file = Path.Combine(dir, "PROJECT")
            Terrabuild.Configuration.FrontEnd.Project.parseWithSource file (File.ReadAllText file))
        |> Seq.toArray
    let legacy () = configs |> Array.map legacyDependencies
    let current () = configs |> Array.map (fun config -> Dependencies.reflectionFindAll config)
    let expected = legacy ()
    Assert.That((current () = expected), Is.True)
    for _ in 1 .. 4 do legacy () |> ignore; current () |> ignore
    let measure action =
        let allocated = GC.GetTotalAllocatedBytes(true)
        let timer = Stopwatch.StartNew()
        let result = action ()
        timer.Stop()
        let bytes = GC.GetTotalAllocatedBytes(true) - allocated
        Assert.That((result = expected), Is.True)
        timer.Elapsed.TotalMilliseconds, bytes
    for index in 1 .. 9 do
        let before, after =
            if index % 2 = 0 then
                let after = measure current
                measure legacy, after
            else
                let before = measure legacy
                before, measure current
        printfn "CONFIG_DEP_BENCH sample=%d projects=%d beforeMs=%.3f afterMs=%.3f beforeBytes=%d afterBytes=%d" index configs.Length (fst before) (fst after) (snd before) (snd after)
