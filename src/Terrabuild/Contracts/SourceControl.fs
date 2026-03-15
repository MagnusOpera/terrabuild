namespace Contracts
open System

type LogType =
    | Terminal
    | Markdown of file:string
    | GitHubActions

type Commit = {
    Sha: string
    Message: string
    Author: string
    Email: string
    Timestamp: DateTime
}

type RunInfo = {
    Name: string
    Repository: string
    OtherCommits: Commit list
    RunId: string
    IsTag: bool
    RunAttempt: int
}

type ISourceControl =
    abstract BranchOrTag: string
    abstract Repository: string
    abstract HeadCommit: Commit
    abstract CommitLog: Commit list
    abstract Run: RunInfo option
    abstract LogTypes: LogType list
    abstract LogError: string -> unit
