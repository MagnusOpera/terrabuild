module Terrabuild.Extensions.Tests.Playwright

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers


// ------------------------------------------------------------------------------------------------

[<Test>]
let ``test cacheability``() =
    getCacheInfo<Playwright> "test" |> should equal Cacheability.Remote

[<Test>]
let ``test some``() =
    let expected =
        [ shellOp("npx", "playwright test --browser webkit --project my-project --opt1 --opt2") ]

    Playwright.test (Some "webkit") // browser
                    (Some "my-project") // project
                    someArgs
    |> normalize
    |> should equal expected

[<Test>]
let ``test none``() =
    let expected =
        [ shellOp("npx", "playwright test") ]

    Playwright.test None // browser
                    None // project
                    noneArgs
    |> normalize
    |> should equal expected
