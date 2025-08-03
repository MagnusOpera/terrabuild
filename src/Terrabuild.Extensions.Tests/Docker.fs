module Terrabuild.Extensions.Tests.Docker

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers


// ------------------------------------------------------------------------------------------------

[<Test>]
let ``__dispatch__ cacheability``() =
    getCacheInfo<Docker> "__dispatch__"
    |> should equal Cacheability.Never

[<Test>]
let ``__dispatch__ some``() =
    let expected =
        [ shellOp("docker", "ci-command --opt1 --opt2") ]

    Docker.__dispatch__ ciContext someArgs
    |> normalize
    |> should equal expected

[<Test>]
let ``__dispatch__ none``() =
    let expected =
        [ shellOp("docker", "local-command") ]

    Docker.__dispatch__ localContext noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``build cacheability``() =
    getCacheInfo<Docker> "build" |> should equal Cacheability.Remote

[<Test>]
let ``build some ci``() =
    let expected =
        [ shellOp("docker", "build --file my-docker-file --tag ghcr.io/magnusopera/test:ABCDEF123456789 --build-arg prm1=\"val1\" --build-arg prm2=\"val2\" --platform linux/arm64,linux/amd64 --opt1 --opt2 .")
          shellOp("docker", "push ghcr.io/magnusopera/test:ABCDEF123456789") ]

    Docker.build ciContext
                 "ghcr.io/magnusopera/test" // image
                 (Some "my-docker-file") // dockerfile
                 (Some ["linux/arm64"; "linux/amd64"]) // platforms
                 someMap // build_args
                 someArgs
    |> normalize
    |> should equal expected

[<Test>]
let ``build some local``() =
    let expected =
        [ shellOp("docker", "build --file my-docker-file --tag ghcr.io/magnusopera/test:123456789ABCDEF --build-arg prm1=\"val1\" --build-arg prm2=\"val2\" --platform linux/arm64,linux/amd64 --opt1 --opt2 .") ]

    Docker.build localContext
                 "ghcr.io/magnusopera/test" // image
                 (Some "my-docker-file") // dockerfile
                 (Some ["linux/arm64"; "linux/amd64"]) // platforms
                 someMap // build_args
                 someArgs
    |> normalize
    |> should equal expected

[<Test>]
let ``build none``() =
    let expected =
        [ shellOp("docker", "build --file Dockerfile --tag ghcr.io/magnusopera/test:ABCDEF123456789 .")
          shellOp("docker", "push ghcr.io/magnusopera/test:ABCDEF123456789") ]

    Docker.build ciContext
                 "ghcr.io/magnusopera/test" // image
                 None // dockerfile
                 None // platforms
                 noneMap // build_args
                 noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``push cacheability``() =
    getCacheInfo<Docker> "push" |> should equal Cacheability.Remote


[<Test>]
let ``push some ci``() =
    let expected =
        [ shellOp("docker", "buildx imagetools create -t ghcr.io/magnusopera/test:my-tag ghcr.io/magnusopera/test:ABCDEF123456789 --opt1 --opt2") ]

    Docker.push ciContext
                "ghcr.io/magnusopera/test" // image
                "my-tag" // tag
                someArgs
    |> normalize
    |> should equal expected

[<Test>]
let ``push some local``() =
    let expected =
        [ shellOp("docker", "tag ghcr.io/magnusopera/test:123456789ABCDEF ghcr.io/magnusopera/test:my-tag --opt1 --opt2") ]

    Docker.push localContext
                "ghcr.io/magnusopera/test" // image
                "my-tag" // tag
                someArgs
    |> normalize
    |> should equal expected

[<Test>]
let ``push none``() =
    let expected =
        [ shellOp("docker", "tag ghcr.io/magnusopera/test:123456789ABCDEF ghcr.io/magnusopera/test:my-tag") ]

    Docker.push localContext
                "ghcr.io/magnusopera/test" // image
                "my-tag" // tag
                noneArgs
    |> normalize
    |> should equal expected
