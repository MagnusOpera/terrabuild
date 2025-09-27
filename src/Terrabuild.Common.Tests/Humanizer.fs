module Terrabuild.Tests.Humanizer
open Humanizer
open FsUnit
open NUnit.Framework
open System

[<Test>]
let ``humanize timespan minutes and seconds``() =
    let ts = TimeSpan.FromSeconds(63L).HumanizeAbbreviated()
    ts |> should equal "1m 3s"

[<Test>]
let ``humanize timespan minutes``() =
    let ts = TimeSpan.FromSeconds(60L).HumanizeAbbreviated()
    ts |> should equal "1m"

[<Test>]
let ``humanize timespan zero``() =
    let ts = TimeSpan.Zero.HumanizeAbbreviated()
    ts |> should equal "0s"
