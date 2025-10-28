module Terrabuild.Extensions.Tests.Sentry

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers


// ------------------------------------------------------------------------------------------------

[<Test>]
let ``run cacheability``() =
    getCacheInfo<Sentry> "release" |> should equal Cacheability.External

[<Test>]
let ``run some``() =
    let expected =
        [ shellOpErrorLevel("npx", "--yes --package=sentry-cli -- --org 'org' --project 'project' releases new '12345'", 1)
          shellOpErrorLevel("npx", "--yes --package=sentry-cli -- --org 'org' --project 'project' releases files '12345' upload-sourcemaps dist --rewrite", 1)
          shellOpErrorLevel("npx", "--yes --package=sentry-cli -- --org 'org' --project 'project' releases finalize '12345'", 1) ]

    Sentry.release ciContext
                   (Some "org") // org
                   (Some "project") // project
                   (Some "12345") // version
    |> normalize
    |> should equal expected


[<Test>]
let ``run none``() =
    let expected =
        [ shellOpErrorLevel("npx", "--yes --package=sentry-cli -- releases new 'ABCDEF123456789'", 1)
          shellOpErrorLevel("npx", "--yes --package=sentry-cli -- releases files 'ABCDEF123456789' upload-sourcemaps dist --rewrite", 1)
          shellOpErrorLevel("npx", "--yes --package=sentry-cli -- releases finalize 'ABCDEF123456789'", 1) ]

    Sentry.release ciContext
                   None // org
                   None // project
                   None // version
    |> normalize
    |> should equal expected
