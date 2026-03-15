namespace SourceControls
open Environment

module GitHubEventReader =
    open System

    type GitHubAuthor =
        { Email: string
          Name: string }

    type GitHubCommit =
        { Author: GitHubAuthor
          Id: string
          Message: string
          Timestamp: DateTime }

    type GitHubEvent =
        { After: string option
          Before: string option
          Commits: GitHubCommit list option }

    let read (filename: string) =
        let json = filename |> IO.readTextFile
        Json.Deserialize<GitHubEvent> json

    let findOtherCommits (filename: string) =
        let event = read filename
        match event.Commits with
        | Some commits ->
            commits
            |> List.map (fun commit -> 
                { Contracts.Commit.Sha = commit.Id
                  Contracts.Commit.Message = commit.Message
                  Contracts.Commit.Author = commit.Author.Name
                  Contracts.Commit.Email = commit.Author.Email
                  Contracts.Commit.Timestamp = commit.Timestamp })
        | _ -> []


type GitHub() =
    let refName = "GITHUB_REF_NAME" |> envVar |> Option.get
    let refType = "GITHUB_REF_TYPE" |> envVar |> Option.get
    let stepSummary = "GITHUB_STEP_SUMMARY" |> envVar |> Option.get
    let runId = "GITHUB_RUN_ID" |> envVar |> Option.get
    let repository = "GITHUB_REPOSITORY" |> envVar |> Option.get
    let runAttempt = "GITHUB_RUN_ATTEMPT" |> envVar  |> Option.get |> int
    let commitLog = currentDir() |> Git.getCommitLog
    let commit = commitLog.Head
    let otherCommits = "GITHUB_EVENT_PATH" |> envVar  |> Option.get |> GitHubEventReader.findOtherCommits |> List.ofSeq

    static member Detect() =
        "GITHUB_ACTION" |> envVar |> Option.isSome

    interface Contracts.ISourceControl with
        override _.BranchOrTag = refName
        override _.Repository = repository
        
        override _.HeadCommit =
            { Sha = commit.Sha
              Message = commit.Subject
              Author = commit.Author
              Email = commit.Email
              Timestamp = commit.Timestamp }
        
        override _.CommitLog =
            commitLog.Tail 
            |> List.map (fun commit -> 
                { Sha = commit.Sha
                  Message = commit.Subject
                  Author = commit.Author
                  Email = commit.Email
                  Timestamp = commit.Timestamp })
  
        override _.Run = 
            Some { Name = "GitHub"
                   IsTag = refType = "tag"
                   RunId = runId
                   OtherCommits = otherCommits
                   Repository = repository
                   RunAttempt = runAttempt }

        override _.LogTypes = [ Contracts.LogType.Markdown stepSummary; Contracts.LogType.GitHubActions ]

        override _.LogError msg = $"::error::{msg}" |> Terminal.writeLine
