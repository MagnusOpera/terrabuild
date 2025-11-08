namespace Terrabuild.Configuration.AST
open Terrabuild.Expressions

type ExtensionBlock =
    { Image: Expr option
      Platform: Expr option
      Variables: Expr option
      Script: Expr option
      Defaults: Map<string, Expr> option
      Batch: Expr option }
