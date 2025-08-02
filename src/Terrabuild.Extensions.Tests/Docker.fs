module Terrabuild.Extensions.Tests.Docker

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers


[<Test>]
let ``build some ci``() =
    let expected =
        execRequest Cacheability.Remote
                    [ shellOp("docker", "build --file my-docker-file --tag ghcr.io/magnusopera/test:ABCDEF123456789 --build-arg arg1=\"value1\" --build-arg arg2=\"value2\" --platform linux/arm64,linux/amd64 \"--opt1\" \"--opt2\" .")
                      shellOp("docker", "push ghcr.io/magnusopera/test:ABCDEF123456789") ]

    Docker.build ciContext
                 "ghcr.io/magnusopera/test"
                 (Some "my-docker-file")
                 (Some ["linux/arm64"; "linux/amd64"])
                 (["arg1", "value1"; "arg2", "value2"] |> Map |> Some)
                 someArgs
    |> normalize
    |> should equal expected

[<Test>]
let ``build some local``() =
    let expected =
        execRequest Cacheability.Local
                    [ shellOp("docker", "build --file my-docker-file --tag ghcr.io/magnusopera/test:123456789ABCDEF --build-arg arg1=\"value1\" --build-arg arg2=\"value2\" --platform linux/arm64,linux/amd64 \"--opt1\" \"--opt2\" .") ]

    Docker.build localContext
                 "ghcr.io/magnusopera/test"
                 (Some "my-docker-file")
                 (Some ["linux/arm64"; "linux/amd64"])
                 (["arg1", "value1"; "arg2", "value2"] |> Map |> Some)
                 someArgs
    |> normalize
    |> should equal expected

[<Test>]
let ``build none``() =
    let expected =
        execRequest Cacheability.Remote
                    [ shellOp("docker", "build --file Dockerfile --tag ghcr.io/magnusopera/test:ABCDEF123456789 .")
                      shellOp("docker", "push ghcr.io/magnusopera/test:ABCDEF123456789") ]

    Docker.build ciContext
                 "ghcr.io/magnusopera/test"
                 None
                 None
                 None
                 noneArgs
    |> normalize
    |> should equal expected




[<Test>]
let ``push some ci``() =
    let expected =
        execRequest Cacheability.Remote
                    [ shellOp("docker", "buildx imagetools create -t ghcr.io/magnusopera/test:my-tag ghcr.io/magnusopera/test:ABCDEF123456789 \"--opt1\" \"--opt2\"") ]

    Docker.push ciContext
                "ghcr.io/magnusopera/test"
                "my-tag"
                someArgs
    |> normalize
    |> should equal expected

[<Test>]
let ``push some local``() =
    let expected =
        execRequest Cacheability.Local
                    [ shellOp("docker", "tag ghcr.io/magnusopera/test:123456789ABCDEF ghcr.io/magnusopera/test:my-tag \"--opt1\" \"--opt2\"") ]

    Docker.push localContext
                "ghcr.io/magnusopera/test"
                "my-tag"
                someArgs
    |> normalize
    |> should equal expected

[<Test>]
let ``push none``() =
    let expected =
        execRequest Cacheability.Local
                    [ shellOp("docker", "tag ghcr.io/magnusopera/test:123456789ABCDEF ghcr.io/magnusopera/test:my-tag") ]

    Docker.push localContext
                "ghcr.io/magnusopera/test"
                "my-tag"
                noneArgs
    |> normalize
    |> should equal expected

