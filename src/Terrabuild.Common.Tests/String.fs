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
let ``slugify path``() =
    "libs/project.dir/path123"
    |> String.slugify
    |> should equal "libs-project-dir-path123"

    "./libs/project.dir/path123/"
    |> String.slugify
    |> should equal "libs-project-dir-path123"

[<Test>]
let ``convert shell args empty``() =
    "  "
    |> String.splitShellArgs
    |> should be Empty

[<Test>]
let ``convert shell args``() =
    "--arg1   --arg2   tagada"
    |> String.splitShellArgs
    |> should equal [ "--arg1"; "--arg2"; "tagada" ]

[<Test>]
let ``convert shell args spaces``() =
    "--arg1  \"some value with spaces\"  tagada"
    |> String.splitShellArgs
    |> should equal [ "--arg1"; "some value with spaces"; "tagada" ]

[<Test>]
let ``convert shell args single quotes preserves spaces``() =
    "sh  -c   'echo   \"hello\"'"
    |> String.splitShellArgs
    |> should equal [ "sh"; "-c"; "echo   \"hello\"" ]

[<Test>]
let ``convert shell args nested single quotes preserves spaces``() =
    "sh  -c   'echo   \"hello\"'"
    |> String.splitShellArgs
    |> should equal [ "sh"; "-c"; "echo   \"hello\"" ]
