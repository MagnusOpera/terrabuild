module Terrabuild.Tests.Scripts.Docker

open FsUnit
open NUnit.Framework
open Terrabuild.ScriptingContracts
open Terrabuild.Tests.Scripts.Helpers

[<Test>]
let ``docker build in ci includes push`` () =
    let context = ciContext "build" (fixtureDir "")
    let args =
        Map.ofList
            [ "image", str "ghcr.io/magnusopera/test"
              "dockerfile", str "Dockerfile"
              "platforms", list [ str "linux/amd64" ] ]

    let result = invokeResult "@docker" "build" context args

    result.Operations
    |> normalizeOps
    |> should equal
        [ op "docker" "build --file Dockerfile --tag ghcr.io/magnusopera/test:ABCDEF --platform linux/amd64 ." 0
          op "docker" "push ghcr.io/magnusopera/test:ABCDEF" 0 ]

[<Test>]
let ``docker push cacheability is external`` () =
    cacheability "@docker" "push" |> should equal (Some Cacheability.External)
