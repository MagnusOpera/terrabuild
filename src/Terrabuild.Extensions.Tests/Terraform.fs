module Terrabuild.Extensions.Tests.Terraform

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers


[<Test>]
let ``dispatch some``() =
    let expected =
        execRequest Cacheability.Never
                    [ shellOp("terraform", "ci-command --opt1 --opt2") ]

    Terraform.__dispatch__ ciContext someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``dispatch none``() =
    let expected =
        execRequest Cacheability.Never
                    [ shellOp("terraform", "local-command") ]

    Terraform.__dispatch__ localContext noneArgs
    |> normalize
    |> should equal expected



[<Test>]
let ``init some``() =
    let expected =
        execRequest Cacheability.Local
                    [ shellOp("terraform", "init -reconfigure -backend-config=gcp-dev --opt1 --opt2") ]

    Terraform.init (Some "gcp-dev")
                   someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``init none``() =
    let expected =
        execRequest Cacheability.Local
                    [ shellOp("terraform", "init -reconfigure") ]

    Terraform.init None
                   noneArgs
    |> normalize
    |> should equal expected



[<Test>]
let ``validate some``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("terraform", "validate --opt1 --opt2") ]

    Terraform.validate someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``validate none``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("terraform", "validate") ]

    Terraform.validate noneArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``select some``() =
    let expected =
        execRequest Cacheability.Local
                    [ shellOp("terraform", "workspace select -or-create dev --opt1 --opt2") ]

    Terraform.select (Some "dev") // workspace
                     (Some true) // create
                     someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``select none``() =
    let expected =
        execRequest Cacheability.Local
                    [ ]

    Terraform.select None // workspace
                     None // create
                     noneArgs
    |> normalize
    |> should equal expected



[<Test>]
let ``plan some``() =
    let expected =
        execRequest (Cacheability.Always ||| Cacheability.Ephemeral)
                    [ shellOp("terraform", "plan -out=terrabuild.planfile -var=\"prm1=val1\" -var=\"prm2=val2\" --opt1 --opt2") ]

    Terraform.plan someMap // variables
                   someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``plan none``() =
    let expected =
        execRequest (Cacheability.Always ||| Cacheability.Ephemeral)
                    [ shellOp("terraform", "plan -out=terrabuild.planfile")]

    Terraform.plan noneMap // variables
                   noneArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``apply some``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("terraform", "apply -input=false --opt1 --opt2") ]

    Terraform.apply (Some true) // no_plan
                    someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``apply none``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("terraform", "apply -input=false terrabuild.planfile")]

    Terraform.apply None // no_plan
                    noneArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``destroy some``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("terraform", "apply -destroy -input=false -var=\"prm1=val1\" -var=\"prm2=val2\" --opt1 --opt2") ]

    Terraform.destroy someMap // no_plan
                      someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``destroy none``() =
    let expected =
        execRequest Cacheability.Always
                    [ shellOp("terraform", "apply -destroy -input=false")]

    Terraform.destroy noneMap // no_plan
                      noneArgs
    |> normalize
    |> should equal expected

