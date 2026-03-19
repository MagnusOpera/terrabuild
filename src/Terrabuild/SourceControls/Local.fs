namespace SourceControls
open Environment

type Local() =
    let commitLog = currentDir() |> Git.getCommitLog
    let commit = commitLog.Head
    let repository =
        currentDir()
        |> Git.tryGetOriginRemote
        |> Option.bind Git.tryNormalizeRepositoryIdentity
        |> Option.defaultValue ""

    interface Contracts.ISourceControl with
        override _.BranchOrTag = currentDir() |> Git.getBranchOrTag
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

        override _.Run = None

        override _.LogTypes = [ Contracts.LogType.Terminal ]
        override _.LogError _ = ()
