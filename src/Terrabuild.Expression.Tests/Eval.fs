module Terrabuild.Expression.Eval.Tests

open NUnit.Framework
open FsUnit
open Terrabuild.Expression
open Errors

let private evaluationContext = {
    Eval.EvaluationContext.WorkspaceDir = Some TestContext.CurrentContext.WorkDirectory
    Eval.EvaluationContext.ProjectDir = FS.combinePath TestContext.CurrentContext.WorkDirectory "project-path" |> Some
    Eval.EvaluationContext.Data = Map.empty
}



[<Test>]
let valueNothing() =
    let expected = Value.Nothing
    let result = eval evaluationContext (Expr.Nothing)
    result |> should equal expected

[<Test>]
let valueString() =
    let expected = Value.String "toto"
    let result = eval evaluationContext (Expr.String "toto")
    result |> should equal expected

[<Test>]
let valueNumber() =
    let expected = Value.Number 42
    let result = eval evaluationContext (Expr.Number 42)
    result |> should equal expected

[<Test>]
let valueEnum() =
    let expected = Value.Enum "tagada"
    let result = eval evaluationContext (Expr.Enum "tagada")
    result |> should equal expected

[<Test>]
let valueBool() =
    let expected = Value.Bool true
    let result = eval evaluationContext (Expr.Bool true)
    result |> should equal expected

[<Test>]
let valueMap() =
    let expected = Value.Map (Map ["hello", Value.String "world"])
    let context = { evaluationContext with Data = Map ["toto", Value.String "world"] }
    let result = eval context (Expr.Map (Map ["hello", Expr.Variable "toto"]))
    result |> should equal expected

[<Test>]
let valueList() =
    let expected = Value.List [Value.String "hello"; Value.String "world"]
    let context = { evaluationContext with Data = Map ["toto", Value.String "world"] }
    let result = eval context (Expr.List [Expr.String "hello"; Expr.Variable "toto"])
    result |> should equal expected

[<Test>]
let valueVariable() =
    let expected = Value.String "titi"
    let context = { evaluationContext with Data = Map ["toto", Value.String "titi"] }
    let result = eval context (Expr.Variable "toto")
    result |> should equal expected

[<Test>]
let concatString() =
    let expected = Value.String "hello world"
    let result = eval evaluationContext (Expr.Function (Function.Plus, [Expr.String "hello"; Expr.String " world"]))
    result |> should equal expected

[<Test>]
let addNumber() =
    let expected = Value.Number 7
    let result = eval evaluationContext (Expr.Function (Function.Plus, [Expr.Number 5; Expr.Number 2]))
    result |> should equal expected

[<Test>]
let addMap() =
    let expected = Value.Map (Map [ "toto", Value.Number 42
                                    "titi", Value.String "pouet" ])
    let result = eval evaluationContext (Expr.Function (Function.Plus, [ Expr.Map (Map [ "toto", Expr.String "pouet"
                                                                                         "titi", Expr.String "pouet" ])
                                                                         Expr.Map (Map [ "toto", Expr.Number 42
                                                                                         "titi", Expr.String "pouet" ]) ]))
    result |> should equal expected

[<Test>]
let addList() =
    let expected = Value.List ([ Value.String "toto"; Value.Number 42 ])
    let result = eval evaluationContext (Expr.Function (Function.Plus, [ Expr.List [Expr.String "toto"]
                                                                         Expr.List [Expr.Number 42] ]))
    result |> should equal expected

[<Test>]
let subNumber() =
    let expected = Value.Number 3
    let result = eval evaluationContext (Expr.Function (Function.Minus, [Expr.Number 5; Expr.Number 2]))
    result |> should equal expected

[<Test>]
let multNumber() =
    let expected = Value.Number 42
    let result = eval evaluationContext (Expr.Function (Function.Mult, [Expr.Number 6; Expr.Number 7]))
    result |> should equal expected

[<Test>]
let divNumber() =
    let expected = Value.Number 3
    let result = eval evaluationContext (Expr.Function (Function.Div, [Expr.Number 21; Expr.Number 7]))
    result |> should equal expected

[<Test>]
let coalesce() =
    let expected = Value.Number 42
    let result = eval evaluationContext (Expr.Function (Function.Coalesce, [Expr.Nothing; Expr.Number 42]))
    result |> should equal expected

[<Test>]
let ternary() =
    let whenTrue = eval evaluationContext (Expr.Function (Function.Ternary, [Expr.Bool true; Expr.String "left"; Expr.String "right"]))
    let whenFalse = eval evaluationContext (Expr.Function (Function.Ternary, [Expr.Bool false; Expr.String "left"; Expr.String "right"]))

    whenTrue |> should equal (Value.String "left")
    whenFalse |> should equal (Value.String "right")

[<Test>]
let trimString() =
    let expected = Value.String "hello"
    let result = eval evaluationContext (Expr.Function (Function.Trim, [Expr.String " hello  "]))
    result |> should equal expected

[<Test>]
let upperString() =
    let expected = Value.String "HELLO"
    let result =
        eval evaluationContext (Expr.Function (Function.Trim,
                                               [Expr.Function (Function.Upper, [ Expr.String " hello  " ])] ))
    result |> should equal expected

[<Test>]
let lowerString() =
    let expected = Value.String "hello"
    let result = eval evaluationContext (Expr.Function (Function.Lower, [ Expr.String "HELLO" ]))
    result |> should equal expected

[<Test>]
let formatList() =
    let expected = Value.String "\\o/THIS42ISAtrueTEMPLATEtiti"

    let context = { evaluationContext
                    with Data = Map ["toto", Value.String "\\o/"] }

    // format("{0}THIS{1}IS{2}A{3}TEMPLATE{4}", $toto, 42, nothing, true, "titi")
    let result =
        eval context (Expr.Function (Function.Format, [
            Expr.String "{0}THIS{1}IS{2}A{3}TEMPLATE{4}"
            Expr.Variable "toto"
            Expr.Number 42
            Expr.Nothing
            Expr.Bool true
            Expr.String "titi" ]))
    result |> should equal expected

[<Test>]
let regexMatch() =
    let result = eval evaluationContext (Expr.Function (Function.RegexMatch, [ Expr.String "^prod(.)*"; Expr.String "prodfr" ]))
    result |> should equal (Value.Bool true)

    let result = eval evaluationContext (Expr.Function (Function.RegexMatch, [ Expr.String "^prod(.)*"; Expr.String "dev" ]))
    result |> should equal (Value.Bool false)

    let result = eval evaluationContext (Expr.Function (Function.RegexMatch, [ Expr.String "^prod(.)*"; Expr.Nothing ]))
    result |> should equal (Value.Bool false)

[<Test>]
let listItem() =
    let expected = Value.Number 42

    let context = { evaluationContext
                    with Data = Map [ 
                        "tagada", Value.List [ Value.String "toto"; Value.Number 42 ]
                    ] }

    let result =
        eval context (Expr.Function (Function.Item, [ Expr.Variable "tagada"; Expr.Number 1]))
    result |> should equal expected

[<Test>]
let mapItem() =
    let expected = Value.Number 42

    let context = { evaluationContext
                    with Data = Map [ 
                        "tagada", Value.Map (Map [ "toto", Value.Number 42 ])
                    ] }

    let result =
        eval context (Expr.Function (Function.Item, [ Expr.Variable "tagada"; Expr.String "toto" ]))
    result |> should equal expected

[<Test>]
let equalValue() =
    let expected = Value.Bool true
    let result = eval evaluationContext (Expr.Function (Function.Equal, [Expr.String "toto"; Expr.String "toto"]))
    result |> should equal expected

[<Test>]
let notEqualValue() =
    let expected = Value.Bool false
    let result = eval evaluationContext (Expr.Function (Function.Equal, [Expr.String "toto"; Expr.Number 42]))
    result |> should equal expected

[<Test>]
let notEqualFunctionValue() =
    let expected = Value.Bool true
    let result = eval evaluationContext (Expr.Function (Function.NotEqual, [Expr.String "toto"; Expr.Number 42]))
    result |> should equal expected

[<Test>]
let toStringValue() =
    let numberValue = eval evaluationContext (Expr.Function (Function.ToString, [Expr.Number 42]))
    let boolValue = eval evaluationContext (Expr.Function (Function.ToString, [Expr.Bool true]))
    let nothingValue = eval evaluationContext (Expr.Function (Function.ToString, [Expr.Nothing]))

    numberValue |> should equal (Value.String "42")
    boolValue |> should equal (Value.String "true")
    nothingValue |> should equal (Value.String "")

[<Test>]
let replaceString() =
    let expected = Value.String "titi titi"
    let result = eval evaluationContext (Expr.Function (Function.Replace, [Expr.String "toto titi"; Expr.String "toto"; Expr.String "titi"]))
    result |> should equal expected

[<Test>]
let countList() =
    let expected = Value.Number 3
    let result = eval evaluationContext (Expr.Function (Function.Count, [Expr.List [Expr.Number 1; Expr.String "toto"; Expr.Bool false]]))
    result |> should equal expected

[<Test>]
let countMap() =
    let expected = Value.Number 1
    let result = eval evaluationContext (Expr.Function (Function.Count, [Expr.Map (Map [ "toto", Expr.Number 42 ])]))
    result |> should equal expected

[<Test>]
let notBool() =
    let result = eval evaluationContext (Expr.Function (Function.Not, [Expr.Bool false]))
    result |> should equal (Value.Bool true)

    let result = eval evaluationContext (Expr.Function (Function.Not, [Expr.Bool true]))
    result |> should equal (Value.Bool false)

    let result = eval evaluationContext (Expr.Function (Function.Not, [Expr.String "toto"]))
    result |> should equal (Value.Bool false)

    let result = eval evaluationContext (Expr.Function (Function.Not, [Expr.Nothing]))
    result |> should equal (Value.Bool true)

    let result = eval evaluationContext (Expr.Function (Function.Not, [Expr.Number 42]))
    result |> should equal (Value.Bool false)

[<Test>]
let andBool() =
    let expected = Value.Bool false
    let result = eval evaluationContext (Expr.Function (Function.And, [Expr.Bool false; Expr.Bool false]))
    result |> should equal expected

    let expected = Value.Bool false
    let result = eval evaluationContext (Expr.Function (Function.And, [Expr.Bool false; Expr.Bool true]))
    result |> should equal expected

    let expected = Value.Bool false
    let result = eval evaluationContext (Expr.Function (Function.And, [Expr.Bool true; Expr.Bool false]))
    result |> should equal expected

    let expected = Value.Bool true
    let result = eval evaluationContext (Expr.Function (Function.And, [Expr.Bool true; Expr.Bool true]))
    result |> should equal expected

    let expected = Value.Bool false
    let result = eval evaluationContext (Expr.Function (Function.And, [Expr.Bool true; Expr.Nothing]))
    result |> should equal expected

[<Test>]
let orBool() =
    let expected = Value.Bool false
    let result = eval evaluationContext (Expr.Function (Function.Or, [Expr.Bool false; Expr.Bool false]))
    result |> should equal expected

    let expected = Value.Bool true
    let result = eval evaluationContext (Expr.Function (Function.Or, [Expr.Bool false; Expr.Bool true]))
    result |> should equal expected

    let expected = Value.Bool true
    let result = eval evaluationContext (Expr.Function (Function.Or, [Expr.Bool true; Expr.Bool false]))
    result |> should equal expected

    let expected = Value.Bool true
    let result = eval evaluationContext (Expr.Function (Function.Or, [Expr.Bool true; Expr.Bool true]))
    result |> should equal expected

    let expected = Value.Bool true
    let result = eval evaluationContext (Expr.Function (Function.Or, [Expr.Nothing; Expr.Bool true]))
    result |> should equal expected

[<Test>]
let conversionFunctions() =
    asStringOption (Value.String "hello") |> should equal (Some "hello")
    asStringOption Value.Nothing |> should equal None
    asString (Value.String "hello") |> should equal "hello"
    match asEnum (Value.Enum "single") with
    | Ok value -> value |> should equal "single"
    | Error _ -> Assert.Fail("Expected enum conversion to succeed")

    match asEnum (Value.String "single") with
    | Error value -> value |> should equal "Failed to convert 'String \"single\"' to enum"
    | Ok _ -> Assert.Fail("Expected enum conversion to fail")
    asBoolOption (Value.Bool true) |> should equal (Some true)
    asBoolOption Value.Nothing |> should equal None
    asStringSetOption (Value.List [ Value.String "a"; Value.String "b" ]) |> should equal (Some (Set [ "a"; "b" ]))

[<Test>]
let mapHelpers() =
    let oldMap = Value.Map (Map [ "left", Value.Number 1 ])
    let newMap = Value.Map (Map [ "right", Value.Number 2 ])

    mapAdd newMap oldMap |> should equal (Value.Map (Map [ "left", Value.Number 1; "right", Value.Number 2 ]))
    asMap oldMap |> should equal (Map [ "left", Value.Number 1 ])

[<Test>]
let locationIsAttachedOnExpressionFailure() =
    let location =
        { SourceLocation.File = Some "WORKSPACE"
          SourceLocation.StartLine = 12
          SourceLocation.StartColumn = 5
          SourceLocation.EndLine = 12
          SourceLocation.EndColumn = 32 }
    let expr =
        Expr.WithLocation location (Expr.Function (Function.Replace, [ Expr.Nothing; Expr.String "x"; Expr.String "y" ]))

    try
        eval evaluationContext expr |> ignore
        Assert.Fail("Expected TerrabuildException")
    with
    | :? TerrabuildException as ex ->
        ex.Location |> should equal (Some location)
        ex.Message |> should equal "Invalid arguments for function Replace with parameters (nothing*string*string)"
