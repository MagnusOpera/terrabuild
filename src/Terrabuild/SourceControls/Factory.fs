module SourceControls.Factory
open Errors
open Environment

let create (): Contracts.ISourceControl =
    if GitHub.Detect() then GitHub()
    else
        let workspace = currentDir()
        if workspace |> Git.isRepository then Local()
        else raiseInvalidArg $"Current workspace '{workspace}' is not a git repository"
