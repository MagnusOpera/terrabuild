module Terrabuild.Extensions.Tests.Sentry

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers


// ------------------------------------------------------------------------------------------------

[<Test>]
let ``sourcemaps cacheability``() =
    getCacheInfo<Sentry> "sourcemaps" |> should equal Cacheability.External

[<Test>]
let ``sourcemaps some``() =
    let expected =
        [ shellOp("sentry-cli", "sourcemaps inject path")
          shellOp("sentry-cli", "sourcemaps upload --project project path") ]

    Sentry.sourcemaps (Some "project") // project
                      (Some "path") // path
    |> normalize
    |> should equal expected


[<Test>]
let ``sourcemaps none``() =
    let expected =
        [ shellOp("sentry-cli", "sourcemaps inject dist")
          shellOp("sentry-cli", "sourcemaps upload dist") ]

    Sentry.sourcemaps None // project
                      None // path
    |> normalize
    |> should equal expected
