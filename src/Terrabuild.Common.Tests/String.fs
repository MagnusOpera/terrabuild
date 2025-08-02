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
