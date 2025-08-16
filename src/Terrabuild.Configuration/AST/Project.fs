namespace Terrabuild.Configuration.AST.Project
open Terrabuild.Configuration.AST
open Terrabuild.Expressions


[<RequireQualifiedAccess>]
type ProjectBlock =
    { Id: string option
      Initializers: Set<string>
      DependsOn: Set<string> option
      Dependencies: Expr option
      Outputs: Expr option
      Ignores: Expr option
      Includes: Expr option
      Labels: Set<string> }


type Step =
    { Extension: string
      Command: string
      Parameters: Map<string, Expr> }

[<RequireQualifiedAccess>]
type TargetBlock =
    { Outputs: Expr option
      DependsOn: Set<string> option
      Rebuild: Expr option
      Ephemeral: Expr option
      Restore: Expr option
      Cache: Expr option
      Deferred: Expr option
      Steps: Step list }

[<RequireQualifiedAccess>]
type ProjectFile =
    { Project: ProjectBlock
      Extensions: Map<string, ExtensionBlock>
      Targets: Map<string, TargetBlock>
      Locals: Map<string, Expr> }

