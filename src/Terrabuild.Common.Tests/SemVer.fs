module Terrabuild.Common.Tests.SemVer
open FsUnit
open NUnit.Framework
open System


[<Test>]
let ``check version parsing``() =
    let expectedStable: Version * string option = (Version("0.174.0"), None)
    "0.174.0"
    |> SemVer.parseSemver
    |> should equal expectedStable

    let expectedNext: Version * string option = (Version("0.174.0"), Some "next")
    "0.174.0-next"
    |> SemVer.parseSemver
    |> should equal expectedNext

    (fun () -> "" |> SemVer.parseSemver |> ignore)
    |> should (throwWithMessage "Invalid version specification ''") typeof<Errors.TerrabuildException>

[<Test>]
let ``check minimal version``() =
    "0.174.0-next" |> SemVer.isAtLeast "0.174.0" |> should be False
    "0.174.0" |> SemVer.isAtLeast "0.174.0-next" |> should be True
    "0.175.0" |> SemVer.isAtLeast "0.174.0" |> should be True
    "0.175.0" |> SemVer.isAtLeast "0.174.0-next" |> should be True
