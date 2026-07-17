namespace Terrabuild.Configuration.AST.Workspace
open Terrabuild.Configuration.AST
open Terrabuild.Expression



[<RequireQualifiedAccess>]
type WorkspaceBlock =
    { Id: string option
      Ignores: Set<string> option
      Deny: Set<string> option
      Version: string option
      Engine: string option
      Configuration: string option
      Environment: string option }

[<RequireQualifiedAccess>]
type TargetBlock =
    { Outputs: Expr option
      DependsOn: Set<string> option
      Build: Expr option
      Cache: Expr option
      Batch: Expr option
      Phase: Expr option }

[<RequireQualifiedAccess>]
type PhaseBlock =
    { DependsOn: Set<string> }

[<RequireQualifiedAccess>]
type WorkspaceFile =
    { Workspace: WorkspaceBlock
      Targets: Map<string, TargetBlock>
      Phases: Map<string, PhaseBlock>
      Variables: Map<string, Expr option>
      Locals: Map<string, Expr>
      Extensions: Map<string, ExtensionBlock> }
