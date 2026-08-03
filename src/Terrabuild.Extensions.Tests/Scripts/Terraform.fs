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

[<Test>]
let ``terraform output captures stdout when requested`` () =
    let context = localContext "output" (fixtureDir "")
    let args =
        Map [ "args", str "-json"
              "stdout", str "terraform-outputs.json" ]
    let result = invokeResult "@terraform" "output" context args

    result.Operations
    |> normalizeOps
    |> should equal
        [ { ShellOperation.Command = "terraform"
            ShellOperation.Arguments = "output -json"
            ShellOperation.ErrorLevel = 0
            ShellOperation.Stdout = Some "terraform-outputs.json" } ]

[<Test>]
let ``terraform output is never cached`` () =
    cacheability "@terraform" "output" |> should equal (Some Cacheability.Never)
