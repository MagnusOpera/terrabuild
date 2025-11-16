#if TERRABUILD_SCRIPT
#r "Terrabuild.Extensibility.dll"
#endif

module VSSolution
open Terrabuild.Extensibility
open System.IO
open System.Text.RegularExpressions

/// <summary>
/// Infers Terrabuild dependencies from the first `.sln` file in the directory by reading project references.
/// Useful for bootstrapping graphs when only a Visual Studio solution is present.
/// </summary>
let private (|Regex|_|) pattern input =
    let m = Regex.Match(input, pattern)
    if m.Success then Some(List.tail [ for g in m.Groups -> g.Value ])
    else None

/// <summary>
/// Discovers project file dependencies from an existing solution and returns project metadata.
/// </summary>
let __defaults__ () =
    let dependencies =
        Directory.EnumerateFiles("*.sln") |> Seq.head
        |> File.ReadLines
        |> Seq.choose (fun line ->
            match line with
            | Regex "Project\(.*\) = \".*\", \"(.*)\", .*" [projectFile] -> Some projectFile
            | _ -> None)
        |> Set.ofSeq

    { ProjectInfo.Default
      with Ignores = Set.empty
           Outputs = Set.empty
           Dependencies = set dependencies }
