module ConfigOptions
open System
open Collections
open Contracts

[<RequireQualifiedAccess>]
type Options = {
    Workspace: string
    HomeDir: string
    TmpDir: string
    SharedDir: string
    WhatIf: bool
    Debug: bool
    MaxConcurrency: int
    Force: bool
    Retry: bool
    LocalOnly: bool
    StartedAt: DateTime
    Targets: string set
    Configuration: string option
    Environment: string option
    LogTypes: Contracts.LogType list
    Note: string option
    Label: string option
    Types: string set option
    Labels: string set option
    Projects: string set option
    Variables: Map<string, string>
    Engine: string option

    // from SourceControl
    BranchOrTag: string
    HeadCommit: Commit
    CommitLog: Commit list
    Run: Contracts.RunInfo option
}
