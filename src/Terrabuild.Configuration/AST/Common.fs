namespace Terrabuild.Configuration.AST
open Terrabuild.Expression

type ExtensionBlock =
    { Image: Expr option
      Platform: Expr option
      Variables: Expr option
      Script: Expr option
      Cpus: Expr option
      Defaults: Map<string, Expr> option
      Env: Map<string, Expr> option }
