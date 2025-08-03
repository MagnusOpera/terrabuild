
module CargoHelpers
    open System.IO

    let findProjectFile dir = FS.combinePath dir "Cargo.toml"

    let findDependencies (projectFile: string) =
        projectFile
        |> File.ReadAllLines
        |> Seq.choose (fun line ->
            match line with
            | String.Regex "path *= *\"(.*)\"" [path] -> Some path
            | _ -> None)
        |> Set.ofSeq

