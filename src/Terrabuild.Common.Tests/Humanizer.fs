module Terrabuild.Tests.Humanizer
open Humanizer
open FsUnit
open NUnit.Framework
open System

[<Test>]
let ``humanize timespan``() =
    let ts = TimeSpan.FromSeconds(63L).HumanizeAbbreviated()
    ts |> should equal "1m 3s"

[<Test>]
let ``humanize timespan zero``() =
    let ts = TimeSpan.Zero.HumanizeAbbreviated()
    ts |> should equal "0s"
