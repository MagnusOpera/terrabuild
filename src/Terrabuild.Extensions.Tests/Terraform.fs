module Terrabuild.Extensions.Tests.Terraform

open NUnit.Framework
open Terrabuild.Extensions
open FsUnit
open Terrabuild.Extensibility
open TestHelpers


// ------------------------------------------------------------------------------------------------

[<Test>]
let ``__dispatch__ cacheability``() =
    getCacheInfo<Terraform> "__dispatch__" |> should equal Cacheability.Never

[<Test>]
let ``__dispatch__ some``() =
    let expected =
        [ shellOp("terraform", "ci-command --opt1 --opt2") ]

    Terraform.__dispatch__ ciContext someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``__dispatch__ none``() =
    let expected =
        [ shellOp("terraform", "local-command") ]

    Terraform.__dispatch__ localContext noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``init cacheability``() =
    getCacheInfo<Terraform> "init" |> should equal Cacheability.Local

[<Test>]
let ``init some``() =
    let expected =
        [ shellOp("terraform", "init -reconfigure -backend-config=gcp-dev --opt1 --opt2") ]

    Terraform.init (Some "gcp-dev")
                   someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``init none``() =
    let expected =
        [ shellOp("terraform", "init -reconfigure") ]

    Terraform.init None
                   noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``validate cacheability``() =
    getCacheInfo<Terraform> "validate" |> should equal Cacheability.Remote

[<Test>]
let ``validate some``() =
    let expected =
        [ shellOp("terraform", "validate --opt1 --opt2") ]

    Terraform.validate someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``validate none``() =
    let expected =
        [ shellOp("terraform", "validate") ]

    Terraform.validate noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``select cacheability``() =
    getCacheInfo<Terraform> "select" |> should equal Cacheability.Never

[<Test>]
let ``select some``() =
    let expected =
        [ shellOp("terraform", "workspace select -or-create dev --opt1 --opt2") ]

    Terraform.select (Some "dev") // workspace
                     (Some true) // create
                     someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``select none``() =
    Terraform.select None // workspace
                     None // create
                     noneArgs
    |> normalize
    |> should be Empty

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``plan cacheability``() =
    getCacheInfo<Terraform> "plan" |> should equal Cacheability.Ephemeral

[<Test>]
let ``plan some``() =
    let expected =
        [ shellOp("terraform", "plan -out=terrabuild.planfile -var=\"prm1=val1\" -var=\"prm2=val2\" --opt1 --opt2") ]

    Terraform.plan someMap // variables
                   someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``plan none``() =
    let expected =
        [ shellOp("terraform", "plan -out=terrabuild.planfile")]

    Terraform.plan noneMap // variables
                   noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``apply cacheability``() =
    getCacheInfo<Terraform> "apply" |> should equal Cacheability.Remote

[<Test>]
let ``apply some``() =
    let expected =
        [ shellOp("terraform", "apply -input=false --opt1 --opt2") ]

    Terraform.apply (Some true) // no_plan
                    someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``apply none``() =
    let expected =
        [ shellOp("terraform", "apply -input=false terrabuild.planfile")]

    Terraform.apply None // no_plan
                    noneArgs
    |> normalize
    |> should equal expected

// ------------------------------------------------------------------------------------------------

[<Test>]
let ``destroy cacheability``() =
    getCacheInfo<Terraform> "destroy" |> should equal Cacheability.Remote

[<Test>]
let ``destroy some``() =
    let expected =
        [ shellOp("terraform", "destroy -input=false -var=\"prm1=val1\" -var=\"prm2=val2\" --opt1 --opt2") ]

    Terraform.destroy someMap // no_plan
                      someArgs
    |> normalize
    |> should equal expected


[<Test>]
let ``destroy none``() =
    let expected =
        [ shellOp("terraform", "destroy -input=false")]

    Terraform.destroy noneMap // no_plan
                      noneArgs
    |> normalize
    |> should equal expected
