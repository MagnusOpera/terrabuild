module Terrabuild.Extensions.Tests.OpenApi

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers


[<Test>]
let ``generate some``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("docker-entrypoint.sh", "generate -i api.json -g typescript-axios -o src/api/client --additional-properties={x} --opt1 --opt2") ]

    OpenApi.generate "typescript-axios" // generator
                     "api.json" // input
                     "src/api/client" // output
                     (["withoutPrefixEnums", "true"] |> Map |> Some)
                     someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``generate none``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("docker-entrypoint.sh", "generate -i api.json -g typescript-axios -o src/api/client") ]

    OpenApi.generate "typescript-axios" // generator
                     "api.json" // input
                     "src/api/client" // output
                     None
                     noneArgs
    |> normalize
    |> should equal expected
