namespace Terrabuild.Configuration.AST.Project
open Terrabuild.Configuration.AST
open Terrabuild.Expression


[<RequireQualifiedAccess>]
type ProjectBlock =
    { Type: string option
      Name: string option
      Initializers: Set<string>
      DependsOn: Set<string> option
      Outputs: Expr option
      Ignores: Expr option
      Includes: Expr option
      Labels: Set<string>
      Environments: Expr option }


type Step =
    { Extension: string
      Command: string
      Parameters: Map<string, Expr> }

[<RequireQualifiedAccess>]
type TargetBlock =
    { Outputs: Expr option
      DependsOn: Set<string> option
      Build: Expr option
      Cache: Expr option
      Batch: Expr option
      Steps: Step list }

[<RequireQualifiedAccess>]
type ProjectFile =
    { Project: ProjectBlock
      Extensions: Map<string, ExtensionBlock>
      Targets: Map<string, TargetBlock>
      Locals: Map<string, Expr> }

