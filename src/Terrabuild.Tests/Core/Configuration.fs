module Terrabuild.Tests.Core.Configuration
open Collections
open FsUnit
open NUnit.Framework
open System
open System.IO
open Errors
open Contracts

let private baseOptions workspace targets =
    { ConfigOptions.Options.Workspace = workspace
      ConfigOptions.Options.HomeDir = workspace
      ConfigOptions.Options.TmpDir = workspace
      ConfigOptions.Options.SharedDir = workspace
      ConfigOptions.Options.WhatIf = true
      ConfigOptions.Options.Debug = false
      ConfigOptions.Options.MaxConcurrency = 2
      ConfigOptions.Options.Force = false
      ConfigOptions.Options.Retry = false
      ConfigOptions.Options.LocalOnly = true
      ConfigOptions.Options.StartedAt = DateTime.UtcNow
      ConfigOptions.Options.Targets = targets
      ConfigOptions.Options.Configuration = None
      ConfigOptions.Options.Environment = None
      ConfigOptions.Options.LogTypes = []
      ConfigOptions.Options.Note = None
      ConfigOptions.Options.Label = None
      ConfigOptions.Options.Types = None
      ConfigOptions.Options.Labels = None
      ConfigOptions.Options.Projects = None
      ConfigOptions.Options.Variables = Map.empty
      ConfigOptions.Options.Engine = None
      ConfigOptions.Options.BranchOrTag = "main"
      ConfigOptions.Options.Repository = "acme/repo"
      ConfigOptions.Options.HeadCommit =
        { Commit.Sha = "deadbeef"
          Commit.Author = "test"
          Commit.Email = "test@example.com"
          Commit.Message = "test"
          Commit.Timestamp = DateTime.UtcNow }
      ConfigOptions.Options.CommitLog = []
      ConfigOptions.Options.Run = None }

let private writeFile (root: string) (path: string) (content: string) =
    let full = Path.Combine(root, path)
    match Path.GetDirectoryName(full) with
    | null -> ()
    | directory when String.IsNullOrWhiteSpace(directory) -> ()
    | directory -> Directory.CreateDirectory(directory) |> ignore
    File.WriteAllText(full, content)

let private withTempWorkspace action =
    let root = Path.Combine(Path.GetTempPath(), $"terrabuild-tests-{Guid.NewGuid():N}")
    Directory.CreateDirectory(root) |> ignore

    let gitInit = System.Diagnostics.ProcessStartInfo("git", "init -q")
    gitInit.WorkingDirectory <- root
    gitInit.RedirectStandardError <- true
    gitInit.RedirectStandardOutput <- true

    match System.Diagnostics.Process.Start(gitInit) with
    | null -> raiseBugError "Failed to start git init process"
    | gitProcess ->
        gitProcess.WaitForExit()
        if gitProcess.ExitCode <> 0 then
            let stderr = gitProcess.StandardError.ReadToEnd()
            raiseBugError $"Failed to initialize git repository for test workspace: {stderr}"

    try
        action root
    finally
        if Directory.Exists(root) then Directory.Delete(root, true)

[<Test>]
let ``Matcher``() =
    let scanFolder = Configuration.scanFolders "tests/simple" (Set ["**/node_modules"; "**/.nuxt"; "**/.vscode"])
    scanFolder "tests/simple/.vscode" |> should equal false
    scanFolder "tests/simple/node_modules" |> should equal false
    scanFolder "tests/simple/toto/node_modules" |> should equal false
    scanFolder "tests/simple/toto/.out" |> should equal true
    scanFolder "tests/simple/toto/tagada.txt" |> should equal true
    scanFolder "tests/simple/src" |> should equal true

[<Test>]
let ``Extension script path must stay inside workspace``() =
    let root = Path.Combine(Path.GetTempPath(), $"terrabuild-tests-{Guid.NewGuid():N}")
    let workspace = Path.Combine(root, "workspace")
    Directory.CreateDirectory(workspace) |> ignore

    try
        let loader = Extensions.lazyLoadScript workspace [ ".git" ] "@custom" (Some "../outside.fss")
        (fun () -> loader.Value |> ignore)
        |> should (throwWithMessage $"Script '../outside.fss' is outside workspace '{workspace}'") typeof<TerrabuildException>
    finally
        if Directory.Exists(root) then Directory.Delete(root, true)

[<Test>]
let ``HTTP extension script URL is rejected``() =
    let workspace = Path.GetTempPath()
    let loader = Extensions.lazyLoadScript workspace [ ".git" ] "@custom" (Some "http://example.com/extension.fss")
    (fun () -> loader.Value |> ignore)
    |> should (throwWithMessage "Only HTTPS script URLs are allowed for extension '@custom'") typeof<TerrabuildException>

[<Test>]
let ``Built-in extension script override is rejected``() =
    let workspace = Path.GetTempPath()
    let loader = Extensions.lazyLoadScript workspace [ ".git" ] "@dotnet" (Some "scripts/custom-dotnet.fss")
    (fun () -> loader.Value |> ignore)
    |> should (throwWithMessage "Script override is not allowed for built-in extension '@dotnet'") typeof<TerrabuildException>

[<Test>]
let ``Local extension import cannot escape workspace``() =
    let root = Path.Combine(Path.GetTempPath(), $"terrabuild-tests-{Guid.NewGuid():N}")
    let workspace = Path.Combine(root, "workspace")
    let scripts = Path.Combine(workspace, "scripts")
    Directory.CreateDirectory(scripts) |> ignore

    let entryScript =
        """
import "../../outside.fss" as Outside

[<export>] let run (context: {| Command: string |}) = Outside.value

type ExportFlag =
  | Dispatch
  | Default
  | Never
  | Local
  | External
  | Remote

{ [nameof run] = [Remote] }
"""

    let outsideScript =
        """
let value = "outside"
"""

    try
        let entryFile = Path.Combine(scripts, "main.fss")
        File.WriteAllText(entryFile, entryScript)
        File.WriteAllText(Path.Combine(root, "outside.fss"), outsideScript)

        let loader = Extensions.lazyLoadScript workspace [ ".git" ] "@custom" (Some "scripts/main.fss")
        (fun () -> loader.Value |> ignore)
        |> should (throwWithMessage $"Script import '../../outside.fss' from '{entryFile}' is outside workspace '{workspace}'") typeof<TerrabuildException>
    finally
        if Directory.Exists(root) then Directory.Delete(root, true)

[<Test>]
let ``pnpm project id stays scoped when package has a name but no dependencies`` () =
    withTempWorkspace (fun root ->
        writeFile root "WORKSPACE" """
workspace {
}

target build {
}

extension @pnpm {
}
"""

        writeFile root "src/apidefs/investapi/PROJECT" """
project investapi {
  @pnpm { }
}

target build {
  @pnpm build { }
}
"""

        writeFile root "src/apidefs/investapi/package.json" """
{
  "name": "@matis/investapi",
  "version": "1.0.0"
}
"""

        let _, config = Configuration.read (baseOptions root (Set [ "build" ]))

        let projectIds = config.Projects |> Map.keys |> Set.ofSeq

        projectIds |> should contain "@pnpm#@matis/investapi"
        projectIds |> should not' (contain "workspace/path#src/apidefs/investapi"))

[<Test>]
let ``path-based extension dependencies still resolve to workspace path when resolution is omitted`` () =
    withTempWorkspace (fun root ->
        writeFile root "WORKSPACE" """
workspace {
}

target build {
}

extension @npm {
}
"""

        writeFile root "libs/shared/PROJECT" """
project shared {
  @npm { }
}

target build {
  @npm build { }
}
"""

        writeFile root "libs/shared/package.json" """
{
  "name": "shared",
  "version": "1.0.0"
}
"""

        writeFile root "apps/api/PROJECT" """
project api {
  @npm { }
}

target build {
  @npm build { }
}
"""

        writeFile root "apps/api/package.json" """
{
  "name": "api",
  "version": "1.0.0",
  "dependencies": {
    "shared": "file:../../libs/shared"
  }
}
"""

        let _, config = Configuration.read (baseOptions root (Set [ "build" ]))
        let project = config.Projects["workspace/path#apps/api"]

        project.Dependencies |> should contain "workspace/path#libs/shared")
