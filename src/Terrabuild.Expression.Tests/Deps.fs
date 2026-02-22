module Terrabuild.Expression.Dependencies.Tests

open NUnit.Framework
open FsUnitTyped
open Terrabuild.Expression

[<Test>]
let ``Scalar dependencies``() =
    let expr = Expr.String "toto"
    let expected = Set.empty

    let deps = Dependencies.find expr
    deps |> shouldEqual expected

[<Test>]
let ``Variable dependencies``() =
    let expr = Expr.Variable "toto"
    let expected = Set.singleton "toto"

    let deps = Dependencies.find expr
    deps |> shouldEqual expected

[<Test>]
let ``Variable inside map dependencies``() =
    let expr = Expr.Map (Map [ "toto_field", Expr.Variable "toto"
                               "titi_field", Expr.Variable "titi" ])
    let expected = Set [ "toto"; "titi" ]

    let deps = Dependencies.find expr
    deps |> shouldEqual expected

[<Test>]
let ``Variable inside list dependencies``() =
    let expr = Expr.List [Expr.Variable "toto"; Expr.Variable "titi"]
    let expected = Set [ "toto"; "titi" ]

    let deps = Dependencies.find expr
    deps |> shouldEqual expected

[<Test>]
let ``field dependencies``() =
    let expr = Expr.Function (Function.Item, [Expr.Variable "var.toto"; Expr.String "titi"])
    let expected = Set [ "var.toto" ]

    let deps = Dependencies.find expr
    deps |> shouldEqual expected

[<Test>]
let ``function dependencies``() =
    let expr = Expr.Function (Function.Plus, [Expr.Variable "toto"; Expr.Variable "titi"])
    let expected = Set [ "toto"; "titi" ]

    let deps = Dependencies.find expr
    deps |> shouldEqual expected

[<Test>]
let ``function of field dependencies``() =
    let expr = Expr.Function (Function.Plus, [Expr.Function (Function.Item, [Expr.Variable "var.toto"; Expr.String "tutu"])
                                              Expr.Function (Function.Item, [Expr.Variable "local.titi"; Expr.String "tutu"])])
    let expected = Set ["var.toto"; "local.titi"]

    let deps = Dependencies.find expr
    deps |> shouldEqual expected






type SubBlock =
    { SubBlockExpr: Expr }

type TestBlock =
    { SimpleExpr: Expr
      OptExpr: Expr option
      MapOfExpr: Map<string, Expr>
      OptMapOfExpr: Map<string, Expr> option
      OptMapOfSubBlock: Map<string, SubBlock>
      Content: string }

[<Test>]
let ``reflection find dependencies`` () =
    let value =
        { SimpleExpr = Expr.Variable "var.config"
          OptExpr = Some (Expr.Variable "local.name")
          MapOfExpr = Map [ "toto", Expr.Variable "var.toto" ]
          OptMapOfExpr = Some (Map [ "titi", Expr.Variable "var.titi" ])
          OptMapOfSubBlock = Map [ "titi", { SubBlockExpr = Expr.Variable "target.block" } ]
          Content = "toto" }

    let expected = Set ["var.config"; "local.name"; "var.toto"; "var.titi"; "target.block" ]

    value
    |> reflectionFind
    |> shouldEqual expected

[<Test>]
let ``array dependencies`` () =
    let expr = Expr.List [ Expr.Variable "project.a"; Expr.Variable "project.b" ]
    let expected = Set [ "project.a"; "project.b" ]

    expr
    |> findArrayOfDependencies
    |> shouldEqual expected
