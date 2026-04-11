module Terrabuild.Tests.Scripts.Terraform

open FsUnit
open NUnit.Framework
open Terrabuild.ScriptingContracts
open Terrabuild.Tests.Scripts.Helpers

[<Test>]
let ``terraform defaults expose planfile output`` () =
    let context = localContext "plan" (fixtureDir "")
    let result = invokeDefaults "@terraform" context

    result.Outputs |> should equal (set [ "*.planfile" ])

[<Test>]
let ``terraform apply uses planfile by default`` () =
    let context = localContext "apply" (fixtureDir "")
    let result = invokeResult "@terraform" "apply" context Map.empty

    result.Operations
    |> normalizeOps
    |> should equal [ op "terraform" "apply -input=false terrabuild.planfile" 0 ]

[<Test>]
let ``terraform apply cacheability is remote`` () =
    cacheability "@terraform" "apply" |> should equal (Some Cacheability.Remote)
