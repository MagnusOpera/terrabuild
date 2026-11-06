module Terrabuild.Tests.Api.Endpoint

open FsUnitTyped
open NUnit.Framework

[<Test>]
let ``API defaults to the Insights production endpoint`` () =
    Api.Endpoint.resolve None
    |> string
    |> shouldEqual "https://api.magnusopera.io/terrabuild"

[<Test>]
let ``API endpoint override selects a custom deployment`` () =
    Api.Endpoint.resolve (Some "https://api.custom.example/terrabuild")
    |> string
    |> shouldEqual "https://api.custom.example/terrabuild"
