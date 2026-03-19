module Git
open Errors
open System
open LibGit2Sharp
open System.IO
open System.Text.RegularExpressions

let getBranchOrTag (dir: string) =
    // https://stackoverflow.com/questions/18659425/get-git-current-branch-tag-name
    match Exec.execCaptureOutput dir "git" "symbolic-ref -q --short HEAD" Map.empty with
    | Exec.Success (output, _) -> output |> String.firstLine
    | _ -> 
        match Exec.execCaptureOutput dir "git" "describe --tags --exact-match" Map.empty with
        | Exec.Success (output, _) -> output |> String.firstLine
        | _ -> raiseExternalError "Failed to get branch or tag"

let getHeadCommitMessage (dir: string) =
    match Exec.execCaptureOutput dir "git" "log -1 --pretty=%B" Map.empty with
    | Exec.Success (output, _) -> output
    | _ -> raiseExternalError "Failed to get head commit message"

let getCurrentUser (dir: string) =
    match Exec.execCaptureOutput dir "git" "config user.name" Map.empty with
    | Exec.Success (output, _) -> output |> String.firstLine
    | _ -> raiseExternalError "Failed to get head commit"

let getCommitLog (dir: string) =
    match Exec.execCaptureOutput dir "git" "log -n 10 --pretty=%H%n%s%n%an%n%ae%n%aI" Map.empty with
    | Exec.Success (output, _) ->
        output |> String.getLines
        |> Seq.chunkBySize 5
        |> Seq.map (fun arr -> {| Sha = arr[0]; Subject = arr[1]; Author = arr[2]; Email = arr[3]; Timestamp = DateTime.Parse(arr[4]) |})
        |> List.ofSeq
    | _ -> raiseExternalError "Failed to get commit log"

let tryGetOriginRemote (dir: string) =
    match Exec.execCaptureOutput dir "git" "config --get remote.origin.url" Map.empty with
    | Exec.Success (output, _) -> output |> String.firstLine |> Some |> Option.filter (String.IsNullOrWhiteSpace >> not)
    | _ -> None

let tryNormalizeRepositoryIdentity (repository: string) =
    let normalizePath (path: string) =
        path.Trim().Trim('/')
        |> fun value -> if value.EndsWith(".git", StringComparison.OrdinalIgnoreCase) then value[..value.LastIndexOf(".git") - 1] else value

    let buildRepositoryIdentity (host: string) (path: string) =
        let normalizedHost = host.Trim().ToLowerInvariant()
        let normalizedPath = normalizePath path

        if String.IsNullOrWhiteSpace(normalizedHost) || String.IsNullOrWhiteSpace(normalizedPath) then
            None
        elif normalizedHost = "github.com" then
            let segments =
                normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries)

            if segments.Length >= 2 then
                Some $"{segments[0].ToLowerInvariant()}/{segments[1].ToLowerInvariant()}"
            else
                None
        else
            Some $"{normalizedHost}/{normalizedPath}"

    match repository.Trim() with
    | "" -> None
    | value ->
        let sshMatch = Regex.Match(value, "^git@(?<host>[^:]+):(?<path>.+)$")

        if sshMatch.Success then
            buildRepositoryIdentity sshMatch.Groups["host"].Value sshMatch.Groups["path"].Value
        else
            let mutable uri = Unchecked.defaultof<Uri>
            match Uri.TryCreate(value, UriKind.Absolute, &uri) with
            | true when uri.Host |> String.IsNullOrWhiteSpace |> not ->
                buildRepositoryIdentity uri.Host uri.AbsolutePath
            | _ -> Some value

// workspaceDir: absolute path anywhere inside the repo (ok if it's a nested "workspace")
// projectDir:   path relative to workspaceDir
// returns: absolute file paths in the working tree that are NOT ignored by git
let enumeratedCommittedFiles (workspaceDir: string) (projectDir: string) : string list =
    use repo = new Repository(workspaceDir |> Repository.Discover)
    let repoDir = repo.Info.WorkingDirectory

    let startDir = FS.combinePath workspaceDir projectDir |> Path.GetFullPath

    let isIgnored (absPath: string) =
        let rel = FS.relativePath repoDir absPath
        let relForGit =
            if Directory.Exists absPath then (if rel.EndsWith "/" then rel else rel + "/") else rel
        repo.Ignore.IsPathIgnored(relForGit)

    let results = ResizeArray<string>()
    let stack = Collections.Generic.Stack<string>()
    stack.Push(startDir)

    while stack.Count > 0 do
        let dir = stack.Pop()

        if String.Equals(Path.GetFileName(dir), ".git") then
            () // never descend into .git
        elif not (isIgnored dir) then
            // files
            for f in Directory.EnumerateFiles(dir) do
                if not (isIgnored f) then
                    results.Add(Path.GetFullPath f)
            // subdirs
            for d in Directory.EnumerateDirectories(dir) do
                stack.Push d

    results |> Seq.toList
