module Terrabuild.Tests.Converters
open Converters
open FsUnit
open NUnit.Framework

[<Test>]
let ``concat space``() =
    None |> concat_space |> should equal ""
    ["1"; "2"; "3"] |> Some |> concat_space |> should equal "1 2 3"

[<Test>]
let ``concat concat_quote``() =
    None |> concat_quote |> should equal ""
    ["1"; "2"; "3"] |> Some |> concat_quote |> should equal "\"1\" \"2\" \"3\""

[<Test>]
let ``or default``() =
    None |> or_default "none" |> should equal "none"
    "some" |> Some |> or_default "none" |> should equal "some"

[<Test>]
let ``format space``() =
    None |> format_space (fun item -> $"item{item}") |> should equal ""
    ["1"; "2"; "3"] |> Some |> format_space (fun item -> $"item{item}") |> should equal "item1 item2 item3"

[<Test>]
let ``format comma``() =
    None |> format_comma (fun item -> $"item{item}") |> should equal ""
    ["1"; "2"; "3"] |> Some |> format_comma (fun item -> $"item{item}") |> should equal "item1,item2,item3"

[<Test>]
let ``map true``() =
    None |> map_true "true" |> should equal ""
    false |> Some |> map_true "true" |> should equal ""
    true |> Some |> map_true "true" |> should equal "true"

[<Test>]
let ``map false``() =
    None |> map_false "false" |> should equal "false"
    false |> Some |> map_false "false" |> should equal "false"
    true |> Some |> map_false "false" |> should equal ""

[<Test>]
let ``map default``() =
    None |> map_default (fun item -> $"item{item}") |> should equal ""
    "X" |> Some |> map_default (fun item -> $"item{item}") |> should equal "itemX"
