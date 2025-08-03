module Terrabuild.Extensions.Tests.Npx

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers


// ------------------------------------------------------------------------------------------------

[<Test>]
let ``run cacheability``() =
    getCacheInfo<Npx> "run" |> should equal Cacheability.Local

[<Test>]
let ``run some``() =
    let expected =
        [ shellOp("npx", "--yes -- my-package --opt1 --opt2") ]

    Npx.run "my-package" // package
            someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``run none``() =
    let expected =
        [ shellOp("npx", "--yes -- my-package") ]

    Npx.run "my-package" // package
            noneArgs
    |> normalize
    |> should equal expected
