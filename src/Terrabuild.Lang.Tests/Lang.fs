module Terrabuild.Lang.Tests
open Terrabuild.Lang.AST
open Terrabuild.Expressions
open System.IO
open NUnit.Framework
open FsUnit

[<Test>]
let checkValidSyntax() =
    let expected = 
        { File.Blocks =
            [ { Block.Resource = "toplevelblock"
                Block.Id = None
                Block.Attributes = [ { Attribute.Name = "attribute1"; Attribute.Value = Expr.String "42" }
                                     { Attribute.Name = "attribute2"; Attribute.Value = Expr.Variable "local.value" }]
                Block.Blocks = [ { Block.Resource = "innerblock"
                                   Block.Id = None
                                   Block.Attributes = [ { Attribute.Name = "innerattribute"; Value = Expr.Number 666 }]
                                   Block.Blocks = [] }
                                 { Block.Resource = "innerblock_with_type"
                                   Block.Id = Some "type"
                                   Block.Attributes = [ { Attribute.Name = "inner_attribute"; Attribute.Value = Expr.Number -20 }]
                                   Block.Blocks = [] } ] }
              { Block.Resource = "other_block_with_type"
                Block.Id = Some "type"
                Block.Attributes = []
                Block.Blocks = [] }
              { Block.Resource = "locals"
                Block.Id = None
                Block.Attributes = [ { Attribute.Name = "string"; Attribute.Value = Expr.String "toto" }
                                     { Attribute.Name = "number"; Attribute.Value = Expr.Number 42 }
                                     { Attribute.Name = "negative_number"; Attribute.Value = Expr.Number -42 }
                                     { Attribute.Name = "map"; Attribute.Value = Expr.Map (Map [("a", Expr.Number 42)
                                                                                                ("b", Expr.Number 666)]) }
                                     { Attribute.Name = "list"; Attribute.Value = Expr.List [Expr.String "a"
                                                                                             Expr.String "b"] }
                                     { Attribute.Name = "literal_bool_true"; Attribute.Value = Expr.Bool true }
                                     { Attribute.Name = "literal_bool_false"; Attribute.Value = Expr.Bool false }
                                     { Attribute.Name = "literal_nothing"; Attribute.Value = Expr.Nothing }
                                     { Attribute.Name = "interpolated_string"; Attribute.Value = Expr.Function(Function.ToString,
                                                                                                               [Expr.Function (Function.Format,
                                                                                                                               [Expr.String "{0}{1}"
                                                                                                                                Expr.String "toto "
                                                                                                                                Expr.Function (Function.Plus,
                                                                                                                                               [Expr.Variable "local.var"; Expr.Number 42])])]) }
                                     { Attribute.Name = "data"; Value = Expr.Variable "var.titi" }
                                     { Attribute.Name = "data_index"; Value = Expr.Function (Function.Item, [Expr.Variable "var.toto"; Expr.Number 42]) }
                                     { Attribute.Name = "data_index_name"; Value = Expr.Function (Function.Item, [Expr.Variable "var.toto"; Expr.String "field"]) }
                                     { Attribute.Name = "data_item"; Value = Expr.Function (Function.Item, [Expr.Variable "var.toto"; Expr.String "field"]) }

                                     { Attribute.Name = "bool_equal"; Value = Expr.Function (Function.Equal, [Expr.Number 42; Expr.Number 666]) }
                                     { Attribute.Name = "bool_not_equal"; Attribute.Value = Expr.Function (Function.NotEqual, [Expr.Number 42; Expr.Number 666]) }
                                     { Attribute.Name = "bool_and"; Value = Expr.Function (Function.And, [Expr.Bool true; Expr.Bool false]) }
                                     { Attribute.Name = "bool_or"; Attribute.Value = Expr.Function (Function.Or, [Expr.Bool true; Expr.Bool false]) }
                                     { Attribute.Name = "bool_not"; Attribute.Value = Expr.Function (Function.Not, [Expr.Bool false]) }

                                     { Attribute.Name = "regex"; Attribute.Value = Expr.Function (Function.RegexMatch, [Expr.String "^prod.*"; Expr.String "prodfr"]) }

                                     { Attribute.Name = "expr_math_op"; Attribute.Value = Expr.Function(Function.Minus,
                                                                                                        [Expr.Function(Function.Plus,
                                                                                                                       [Expr.Function (Function.Plus,
                                                                                                                                       [Expr.Number 1
                                                                                                                                        Expr.Function (Function.Mult,
                                                                                                                                                       [Expr.Number 42
                                                                                                                                                        Expr.Number 2])])
                                                                                                                        Expr.Function (Function.Div,
                                                                                                                                       [Expr.Number 4
                                                                                                                                        Expr.Number 4])])
                                                                                                         Expr.Number 3]) }
                                     { Attribute.Name = "expr_bool_op"; Attribute.Value = Expr.Function(Function.Equal,
                                                                                                        [Expr.Function
                                                                                                            (Function.Equal,
                                                                                                             [Expr.Function (Function.Plus,
                                                                                                                             [Expr.Number 1
                                                                                                                              Expr.Number 42])
                                                                                                              Expr.Function (Function.Plus,
                                                                                                                             [Expr.Number 42
                                                                                                                              Expr.Number 1])])
                                                                                                         Expr.Bool false]) }
                                     { Attribute.Name = "coalesce_op"; Attribute.Value = Expr.Function (Function.Coalesce,
                                                                                                        [Expr.Nothing
                                                                                                         Expr.String "toto"]) }
                                     { Attribute.Name = "ternary_op"; Attribute.Value = Expr.Function (Function.Ternary,
                                                                                                       [Expr.Bool true
                                                                                                        Expr.String "titi"
                                                                                                        Expr.String "toto"]) }
                                     { Attribute.Name = "function_trim"; Attribute.Value = Expr.Function (Function.Trim, []) }
                                     { Attribute.Name = "function_upper"; Attribute.Value = Expr.Function (Function.Upper, []) }
                                     { Attribute.Name = "function_lower"; Attribute.Value = Expr.Function (Function.Lower, []) }
                                     { Attribute.Name = "function_replace"; Attribute.Value = Expr.Function (Function.Replace, []) }
                                     { Attribute.Name = "function_count"; Attribute.Value = Expr.Function (Function.Count, []) }
                                     { Attribute.Name = "function_arity0"; Attribute.Value = Expr.Function (Function.Trim, []) }
                                     { Attribute.Name = "function_arity1"; Attribute.Value = Expr.Function (Function.Trim, [Expr.String "titi"]) }
                                     { Attribute.Name = "function_arity2"; Attribute.Value = Expr.Function (Function.Trim, [Expr.String "titi"; Expr.Number 42]) }
                                     { Attribute.Name = "function_arity3"; Value = Expr.Function (Function.Trim, [Expr.String "titi"; Expr.Number 42; Expr.Bool false]) }]
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
let errorRecoveryCollectsMultipleErrors() =
    let content = File.ReadAllText("TestFiles/Error_RecoveryMultiple")
    try
        FrontEnd.parse content |> ignore
        Assert.Fail("Expected parse errors")
    with
    | :? Errors.TerrabuildException as ex ->
        ex.Message |> should contain "Parse errors:"
        ex.Message |> should contain "Parse error at (2,"
        ex.Message |> should contain "invalid attribute name '^badattr'"
