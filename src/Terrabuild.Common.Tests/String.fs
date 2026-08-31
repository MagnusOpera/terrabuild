module Tests.String
open FsUnit
open NUnit.Framework

[<Test>]
let ``Match regex``() =
    let s = "extension"
    let r = 
        match "extension" with
        | String.Regex "(@?[a-z](?:[_-]?[a-z0-9]+)*)" [identifier] -> identifier
        | _ -> Errors.raiseParseError $"Invalid resource name: {s}"
    r |> should equal "extension"


[<Test>]
let ``remove extra shell arg spaces``() =
    "  build   --no-restore --no-dependencies   \"--configuration\"   Debug    "
    |> String.normalizeShellArgs
    |> should equal "build --no-restore --no-dependencies \"--configuration\" Debug"

[<Test>]
let ``split shell args preserves quoted and empty values``() =
    "build \"Project With Spaces.csproj\" --property='A B' \"\" escaped\\ value"
    |> String.splitShellArgs
    |> should equal [ "build"; "Project With Spaces.csproj"; "--property=A B"; ""; "escaped value" ]

[<Test>]
let ``slugify path``() =
    "libs/project.dir/path123"
    |> String.slugify
    |> should equal "libs-project-dir-path123"

    "./libs/project.dir/path123/"
    |> String.slugify
    |> should equal "libs-project-dir-path123"
