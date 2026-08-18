module Terrabuild.Tests.Core.Configuration
open Collections
open FsUnit
open NUnit.Framework
open System
open System.IO
open Errors
open Contracts
open Terrabuild.Configuration.AST
open Terrabuild.Expression

let private baseOptions workspace targets =
    { ConfigOptions.Options.Workspace = workspace
      ConfigOptions.Options.HomeDir = workspace
      ConfigOptions.Options.TmpDir = workspace
      ConfigOptions.Options.SharedDir = workspace
      ConfigOptions.Options.DryRun = true
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
      ConfigOptions.Options.GroupId = None
      ConfigOptions.Options.Label = None
      ConfigOptions.Options.Types = None
      ConfigOptions.Options.Labels = None
      ConfigOptions.Options.Projects = None
      ConfigOptions.Options.Variables = Map.empty
      ConfigOptions.Options.Engine = ConfigOptions.Engine.Host
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
let ``project extension overlay inherits scalars and adds collection entries`` () =
    let inherited =
        { ExtensionBlock.Image = Some (Expr.String "workspace-image")
          ExtensionBlock.Platform = Some (Expr.String "linux/amd64")
          ExtensionBlock.Variables = Some (Expr.List [ Expr.String "WORKSPACE" ])
          ExtensionBlock.Script = Some (Expr.String "workspace.fss")
          ExtensionBlock.Cpus = Some (Expr.Number 2)
          ExtensionBlock.Defaults = Some (Map [ "workspace", Expr.Bool true ])
          ExtensionBlock.Env = Some (Map [ "WORKSPACE", Expr.String "workspace" ]) }

    let declared =
        { ExtensionBlock.Image = None
          ExtensionBlock.Platform = Some (Expr.String "linux/arm64")
          ExtensionBlock.Variables = Some (Expr.List [ Expr.String "PROJECT" ])
          ExtensionBlock.Script = None
          ExtensionBlock.Cpus = None
          ExtensionBlock.Defaults = Some (Map [ "project", Expr.Bool true ])
          ExtensionBlock.Env = Some (Map [ "PROJECT", Expr.String "project" ]) }

    Configuration.overlayExtension "@shell" inherited declared
    |> should equal
        { ExtensionBlock.Image = inherited.Image
          ExtensionBlock.Platform = declared.Platform
          ExtensionBlock.Variables =
            Some (
                Expr.Function (
                    Function.Plus,
                    [ inherited.Variables.Value; declared.Variables.Value ]
                )
            )
          ExtensionBlock.Script = inherited.Script
          ExtensionBlock.Cpus = inherited.Cpus
          ExtensionBlock.Defaults =
            Some (Map [ "project", Expr.Bool true; "workspace", Expr.Bool true ])
          ExtensionBlock.Env =
            Some (
                Map [ "PROJECT", Expr.String "project"
                      "WORKSPACE", Expr.String "workspace" ]
            ) }

[<TestCase("defaults")>]
[<TestCase("env")>]
let ``project extension collections reject inherited key replacement`` field =
    let inherited =
        { ExtensionBlock.Image = None
          ExtensionBlock.Platform = None
          ExtensionBlock.Variables = None
          ExtensionBlock.Script = None
          ExtensionBlock.Cpus = None
          ExtensionBlock.Defaults = Some (Map [ "SHARED", Expr.String "workspace" ])
          ExtensionBlock.Env = Some (Map [ "SHARED", Expr.String "workspace" ]) }

    let entries = Some (Map [ "SHARED", Expr.String "project" ])
    let declared =
        { ExtensionBlock.Image = None
          ExtensionBlock.Platform = None
          ExtensionBlock.Variables = None
          ExtensionBlock.Script = None
          ExtensionBlock.Cpus = None
          ExtensionBlock.Defaults = if field = "defaults" then entries else None
          ExtensionBlock.Env = if field = "env" then entries else None }

    Assert.That(
        Action(fun () -> Configuration.overlayExtension "@shell" inherited declared |> ignore),
        Throws.TypeOf<TerrabuildException>()
            .With.Message.Contains($"cannot replace inherited {field} entries: 'SHARED'")
    )

[<Test>]
let ``project extension specialization adds collection entries`` () =
    withTempWorkspace (fun root ->
        writeFile root "WORKSPACE" """
workspace {}

target build {}

extension @shell {
  image = "workspace-image"
  platform = "linux/amd64"
  cpus = 2
  variables = [ "WORKSPACE_ONE", "WORKSPACE_TWO" ]
  defaults {
    arguments = "workspace"
    workspace_only = true
  }
  env {
    SHARED = "workspace"
    WORKSPACE_ONLY = "workspace"
  }
}
"""

        writeFile root "apps/inherit/PROJECT" """
project inherit { @shell {} }

extension @shell {
  platform = "linux/arm64"
}

target build {
  @shell echo {}
}
"""

        writeFile root "apps/add/PROJECT" """
project add { @shell {} }

extension @shell {
  variables = [ "PROJECT_ONLY" ]
  defaults {
    project_only = true
  }
  env {
    PROJECT_ONLY = "project"
  }
}

target build {
  @shell echo {}
}
"""

        writeFile root "apps/empty/PROJECT" """
project empty { @shell {} }

extension @shell {
  image = nothing
  platform = nothing
  cpus = nothing
  variables = []
  defaults {}
  env {}
}

target build {
  @shell echo {}
}
"""

        let _, config = Configuration.read (baseOptions root (Set [ "build" ]))
        let step project =
            config.Projects[$"workspace/path#apps/{project}"].Targets["build"].Steps
            |> List.exactlyOne

        let inherited = step "inherit"
        inherited.Image |> should equal (Some "workspace-image")
        inherited.Platform |> should equal (Some "linux/arm64")
        inherited.Cpus |> should equal (Some 2)
        inherited.ContainerVariables |> should equal (Set [ "WORKSPACE_ONE"; "WORKSPACE_TWO" ])
        inherited.Envs
        |> should equal (Map [ "SHARED", "workspace"; "WORKSPACE_ONLY", "workspace" ])
        inherited.Context
        |> should equal
            (Value.Map (Map [ "arguments", Value.String "workspace"
                              "workspace_only", Value.Bool true ]))

        let added = step "add"
        added.Image |> should equal (Some "workspace-image")
        added.Platform |> should equal (Some "linux/amd64")
        added.Cpus |> should equal (Some 2)
        added.ContainerVariables
        |> should equal (Set [ "PROJECT_ONLY"; "WORKSPACE_ONE"; "WORKSPACE_TWO" ])
        added.Envs
        |> should equal
            (Map [ "PROJECT_ONLY", "project"
                   "SHARED", "workspace"
                   "WORKSPACE_ONLY", "workspace" ])
        added.Context
        |> should equal
            (Value.Map (Map [ "arguments", Value.String "workspace"
                              "project_only", Value.Bool true
                              "workspace_only", Value.Bool true ]))

        let empty = step "empty"
        empty.Image |> should equal None
        empty.Platform |> should equal None
        empty.Cpus |> should equal None
        empty.ContainerVariables |> should equal (Set [ "WORKSPACE_ONE"; "WORKSPACE_TWO" ])
        empty.Envs
        |> should equal (Map [ "SHARED", "workspace"; "WORKSPACE_ONLY", "workspace" ])
        empty.Context
        |> should equal
            (Value.Map (Map [ "arguments", Value.String "workspace"
                              "workspace_only", Value.Bool true ])))

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
let ``Legacy fsx extension script is rejected``() =
    withTempWorkspace (fun root ->
        writeFile root "scripts/custom.fsx" "let value = 1"

        let loader = Extensions.lazyLoadScript root [ ".git" ] "@custom" (Some "scripts/custom.fsx")
        Assert.That(
            Action(fun () -> loader.Value |> ignore),
            Throws.TypeOf<TerrabuildException>().With.Message.Contains("Legacy F# extension scripts are no longer supported")))

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

[<Test>]
let ``used workspace extension project references are added to project dependencies`` () =
    withTempWorkspace (fun root ->
        writeFile root "WORKSPACE" """
workspace {
}

target build {
}

extension @pnpm {
  image = "ghcr.io/acme/pnpm:${project.pnpm.version}"
}
"""

        writeFile root "toolchains/pnpm/PROJECT" """
project pnpm {
  labels = [ "toolchain" ]
  @docker { }
}

target build {
  @docker build { }
}
"""

        writeFile root "apps/web/PROJECT" """
project web {
  @pnpm { }
}

target build {
  @pnpm build { }
}
"""

        writeFile root "apps/web/package.json" """
{
  "name": "web",
  "version": "1.0.0"
}
"""

        let _, config = Configuration.read (baseOptions root (Set [ "build" ]))
        let project = config.Projects["@pnpm#web"]

        project.Dependencies |> should contain "workspace/path#toolchains/pnpm")

[<Test>]
let ``workspace expressions can compare terrabuild engine with docker enum`` () =
    withTempWorkspace (fun root ->
        writeFile root "WORKSPACE" """
workspace {
}

target build {
  build = terrabuild.engine == ~docker ? ~always : ~lazy
}
"""

        writeFile root "app/PROJECT" """
project app { @shell {} }

target build {
  @shell echo { arguments = "app" }
}
"""

        let options =
            { baseOptions root (Set [ "build" ]) with
                ConfigOptions.Options.Engine = ConfigOptions.Engine.Docker }

        let _, config = Configuration.read options
        let project = config.Projects["workspace/path#app"]

        project.Targets["build"].Build |> should equal (Some GraphDef.BuildMode.Always))

[<Test>]
let ``Configuration rejects project scripts using fsx`` () =
    withTempWorkspace (fun root ->
        writeFile root "WORKSPACE" """
workspace {
}

target build {
}

extension @custom {
  script = "scripts/custom.fsx"
}
"""

        writeFile root "scripts/custom.fsx" "let value = 1"

        writeFile root "app/PROJECT" """
project app {
  @custom { }
}

target build {
  @custom build { }
}
"""

        Assert.That(Action(fun () -> Configuration.read (baseOptions root (Set [ "build" ])) |> ignore), Throws.TypeOf<TerrabuildException>()))
