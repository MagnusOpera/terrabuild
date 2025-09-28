module Terrabuild.Common.Tests.SemVer
open FsUnit
open NUnit.Framework
open System
open Version


[<Test>]
let ``check version parsing``() =
    "0.174.0"
    |> parseSemver
    |> should equal { Major = 0; Minor = 174; Patch = 0; Pre = None }

    "0.174.0-next"
    |> parseSemver
    |> should equal { Major = 0; Minor = 174; Patch = 0; Pre = Some "next" }

    (fun () -> "2." |> parseSemver |> ignore)
    |> should (throwWithMessage "Invalid version specification '2.'") typeof<Errors.TerrabuildException>

[<Test>]
let ``check minimal version``() =
    "0.174.0-next" |> isAtLeast "0.174.0" |> should be False
    "0.174.0" |> isAtLeast "0.174.0-next" |> should be True
    "0.175.0" |> isAtLeast "0.174.0" |> should be True
    "0.175.0" |> isAtLeast "0.174.0-next" |> should be True

    "0.174.0" |> isAtLeast "0.174-next" |> should be True
    "0.174.0" |> isAtLeast "0-next" |> should be True

    "0.175.0" |> isAtLeast "0.174" |> should be True
    "0.175.0" |> isAtLeast "0" |> should be True
