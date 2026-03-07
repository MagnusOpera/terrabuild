module Terrabuild.Lang.Tests
open Terrabuild.Lang.AST
open Terrabuild.Expression
open System.IO
open NUnit.Framework
open FsUnit

let private attr name value =
    { Attribute.Name = name
      Attribute.Operator = AssignmentOperator.Assign
      Attribute.Value = value }

let private attrWith operator name value =
    { Attribute.Name = name
      Attribute.Operator = operator
      Attribute.Value = value }

[<Test>]
let checkValidSyntax() =
    let expected = 
        { File.Blocks =
            [ { Block.Resource = "toplevelblock"
                Block.Id = None
                Block.Attributes = [ attr "attribute1" (Expr.String "42")
                                     attr "attribute2" (Expr.Variable "local.value") ]
                Block.Blocks = [ { Block.Resource = "innerblock"
                                   Block.Id = None
                                   Block.Attributes = [ attr "innerattribute" (Expr.Number 666) ]
                                   Block.Blocks = [] }
                                 { Block.Resource = "innerblock_with_type"
                                   Block.Id = Some "type"
                                   Block.Attributes = [ attr "inner_attribute" (Expr.Number -20) ]
                                   Block.Blocks = [] } ] }
              { Block.Resource = "other_block_with_type"
                Block.Id = Some "type"
                Block.Attributes = []
                Block.Blocks = [] }
              { Block.Resource = "locals"
                Block.Id = None
                Block.Attributes = [ attr "string" (Expr.String "toto")
                                     attr "number" (Expr.Number 42)
                                     attr "negative_number" (Expr.Number -42)
                                     attr "map" (Expr.Map (Map [("a", Expr.Number 42)
                                                                ("b", Expr.Number 666)]))
                                     attr "list" (Expr.List [Expr.String "a"; Expr.String "b"])
                                     attr "literal_bool_true" (Expr.Bool true)
                                     attr "literal_bool_false" (Expr.Bool false)
                                     attr "literal_nothing" Expr.Nothing
                                     attr "interpolated_string" (Expr.Function(Function.ToString,
                                                                              [Expr.Function (Function.Format,
                                                                                              [Expr.String "{0}{1}"
                                                                                               Expr.String "toto "
                                                                                               Expr.Function (Function.Plus,
                                                                                                              [Expr.Variable "local.var"; Expr.Number 42])])]))
                                     attr "data" (Expr.Variable "var.titi")
                                     attr "data_index" (Expr.Function (Function.Item, [Expr.Variable "var.toto"; Expr.Number 42]))
                                     attr "data_index_name" (Expr.Function (Function.Item, [Expr.Variable "var.toto"; Expr.String "field"]))
                                     attr "data_item" (Expr.Function (Function.Item, [Expr.Variable "var.toto"; Expr.String "field"]))

                                     attr "bool_equal" (Expr.Function (Function.Equal, [Expr.Number 42; Expr.Number 666]))
                                     attr "bool_not_equal" (Expr.Function (Function.NotEqual, [Expr.Number 42; Expr.Number 666]))
                                     attr "bool_and" (Expr.Function (Function.And, [Expr.Bool true; Expr.Bool false]))
                                     attr "bool_or" (Expr.Function (Function.Or, [Expr.Bool true; Expr.Bool false]))
                                     attr "bool_not" (Expr.Function (Function.Not, [Expr.Bool false]))

                                     attr "regex" (Expr.Function (Function.RegexMatch, [Expr.String "^prod.*"; Expr.String "prodfr"]))

                                     attr "expr_math_op" (Expr.Function(Function.Minus,
                                                                        [Expr.Function(Function.Plus,
                                                                                       [Expr.Function (Function.Plus,
                                                                                                       [Expr.Number 1
                                                                                                        Expr.Function (Function.Mult,
                                                                                                                       [Expr.Number 42
                                                                                                                        Expr.Number 2])])
                                                                                        Expr.Function (Function.Div,
                                                                                                       [Expr.Number 4
                                                                                                        Expr.Number 4])])
                                                                         Expr.Number 3]))
                                     attr "expr_bool_op" (Expr.Function(Function.Equal,
                                                                        [Expr.Function
                                                                            (Function.Equal,
                                                                             [Expr.Function (Function.Plus,
                                                                                             [Expr.Number 1
                                                                                              Expr.Number 42])
                                                                              Expr.Function (Function.Plus,
                                                                                             [Expr.Number 42
                                                                                              Expr.Number 1])])
                                                                         Expr.Bool false]))
                                     attr "coalesce_op" (Expr.Function (Function.Coalesce, [Expr.Nothing; Expr.String "toto"]))
                                     attr "ternary_op" (Expr.Function (Function.Ternary, [Expr.Bool true; Expr.String "titi"; Expr.String "toto"]))
                                     attr "function_trim" (Expr.Function (Function.Trim, []))
                                     attr "function_upper" (Expr.Function (Function.Upper, []))
                                     attr "function_lower" (Expr.Function (Function.Lower, []))
                                     attr "function_replace" (Expr.Function (Function.Replace, []))
                                     attr "function_count" (Expr.Function (Function.Count, []))
                                     attr "function_arity0" (Expr.Function (Function.Trim, []))
                                     attr "function_arity1" (Expr.Function (Function.Trim, [Expr.String "titi"]))
                                     attr "function_arity2" (Expr.Function (Function.Trim, [Expr.String "titi"; Expr.Number 42]))
                                     attr "function_arity3" (Expr.Function (Function.Trim, [Expr.String "titi"; Expr.Number 42; Expr.Bool false])) ]
                Blocks = [] }] }

    let content = File.ReadAllText("TestFiles/Success_Syntax")
    let file = FrontEnd.parse content

    file |> should equal expected


[<Test>]
let duplicatedAttributeIsError() =
    let content = File.ReadAllText("TestFiles/Error_DuplicatedAttribute")
    (fun () -> FrontEnd.parse content |> ignore) |> should (throwWithMessage "Parse error at (4,1): duplicated attribute 'attribute1'") typeof<Errors.TerrabuildException>

[<Test>]
let unknownFunctionIsError() =
    let content = File.ReadAllText("TestFiles/Error_UnknownFunction")
    (fun () -> FrontEnd.parse content |> ignore) |> should (throwWithMessage "Parse error at (2,26): unknown function 'tagada'") typeof<Errors.TerrabuildException>

[<Test>]
let unknownLiteralIsError() =
    let content = File.ReadAllText("TestFiles/Error_UnknownLiteral")
    (fun () -> FrontEnd.parse content |> ignore) |> should (throwWithMessage "Parse error at (3,1): unknown literal 'tagada'") typeof<Errors.TerrabuildException>

[<Test>]
let invalidResourceName() =
    let content = File.ReadAllText("TestFiles/Error_InvalidResourceName")
    (fun () -> FrontEnd.parse content |> ignore) |> should (throwWithMessage "Parse error at (1,1): invalid resource name '^toto'") typeof<Errors.TerrabuildException>

[<Test>]
let invalidResourceIdentifier() =
    let content = File.ReadAllText("TestFiles/Error_InvalidResourceIdentifier")
    (fun () -> FrontEnd.parse content |> ignore) |> should (throwWithMessage "Parse error at (1,31): invalid resource identifier '^toto'") typeof<Errors.TerrabuildException>

[<Test>]
let invalidAttributeName() =
    let content = File.ReadAllText("TestFiles/Error_InvalidAttributeName")
    (fun () -> FrontEnd.parse content |> ignore) |> should (throwWithMessage "Parse error at (2,15): invalid attribute name '^attribute1'") typeof<Errors.TerrabuildException>

[<Test>]
let invalidScopeIdentifier() =
    let content = File.ReadAllText("TestFiles/Error_InvalidScopeIdentifier")
    (fun () -> FrontEnd.parse content |> ignore) |> should (throwWithMessage "Parse error at (2,22): invalid scope identifier '^toto'") typeof<Errors.TerrabuildException>

[<Test>]
let invalidScopedIdentifier() =
    let content = File.ReadAllText("TestFiles/Error_InvalidScopedIdentifier")
    (fun () -> FrontEnd.parse content |> ignore) |> should (throwWithMessage "Parse error at (2,21): invalid resource identifier '@value'") typeof<Errors.TerrabuildException>

[<Test>]
let parseWithSourceTracksExpressionLocation() =
    let content = File.ReadAllText("TestFiles/Success_Syntax")
    let file = FrontEnd.parseWithSource "TestFiles/Success_Syntax" content

    let firstAttribute = file.Blocks.Head.Attributes.Head
    match Expr.TryGetLocation firstAttribute.Value with
    | Some location ->
        location.File |> should equal (Some "TestFiles/Success_Syntax")
        location.StartLine |> should equal 5
    | None -> Assert.Fail("Expected expression location")

[<Test>]
let outputsOperatorsParseAndPreserveOrder() =
    let content =
        """
project {
  outputs += [ "dist/**" ]
  outputs -= [ "obj/**" ]
  outputs = [ "bin/**" ]
}
"""

    let file = FrontEnd.parse content
    let project = file.Blocks.Head

    project.Attributes
    |> should equal [
        attrWith AssignmentOperator.Add "outputs" (Expr.List [ Expr.String "dist/**" ])
        attrWith AssignmentOperator.Remove "outputs" (Expr.List [ Expr.String "obj/**" ])
        attrWith AssignmentOperator.Assign "outputs" (Expr.List [ Expr.String "bin/**" ])
    ]
