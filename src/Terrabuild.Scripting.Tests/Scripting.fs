module Terrabuild.Scripting.Scripting.Tests

open NUnit.Framework
open FsUnit
open Terrabuild.Scripting
open Terrabuild.ScriptingContracts
open Terrabuild.Expression
open Errors
open System
open System.IO


[<Test>]
let loadScript() =
    let root = NUnit.Framework.TestContext.CurrentContext.TestDirectory
    let script = Terrabuild.Scripting.loadScript root [ "Terrabuild.Scripting.dll" ] "TestFiles/Toto.fsx"
    let invocable = script.GetMethod("Tagada")
    let context = { ExtensionContext.Debug= false; ExtensionContext.Directory = "this is a path"; ExtensionContext.CI = false }
    let args = Value.Map (Map [ "context", Value.Object context])
    let res = invocable.Value.Invoke args
    res |> should equal context.Directory

[<Test>]
let loadScriptWithError() =
    let root = NUnit.Framework.TestContext.CurrentContext.TestDirectory
    (fun () -> Terrabuild.Scripting.loadScript root [ "Terrabuild.Scripting.dll" ] "TestFiles/Failure.fsx" |> ignore)
    |> should (throwWithMessage "Failed to identify function scope (either module or root class 'Failure')") typeof<TerrabuildException>

[<Test>]
let loadVSSolution() =
    let testDir = System.IO.Path.Combine(NUnit.Framework.TestContext.CurrentContext.TestDirectory, "TestFiles")
    let root = NUnit.Framework.TestContext.CurrentContext.TestDirectory
    let script = Terrabuild.Scripting.loadScript root [ "Terrabuild.Scripting.dll" ] "TestFiles/VSSolution.fsx"
    let invocable = script.GetMethod("__defaults__")
    let context = { ExtensionContext.Debug= false; ExtensionContext.Directory = testDir; ExtensionContext.CI = false }
    let args = Value.Map (Map [ "context", Value.Object context])

    let res = invocable.Value.Invoke<Terrabuild.ScriptingContracts.ProjectInfo> args

    let expectedDependencies = Set [ "src"; "src\Terrabuild\Terrabuild.fsproj"; "src\Terrabuild.Configuration\Terrabuild.Configuration.fsproj" ]
    res.Dependencies |> should equal expectedDependencies

[<Test>]
let loadFScriptExports() =
    let root = NUnit.Framework.TestContext.CurrentContext.TestDirectory
    let script = Terrabuild.Scripting.loadScript root [] "TestFiles/Extension.fss"
    script.GetMethod("run").IsSome |> should equal true
    script.GetMethod("hidden").IsNone |> should equal true

[<Test>]
let loadFScriptDispatchAndDefault() =
    let root = NUnit.Framework.TestContext.CurrentContext.TestDirectory
    let script = Terrabuild.Scripting.loadScript root [] "TestFiles/Extension.fss"
    script.ResolveCommandMethod("unknown") |> should equal (Some "dispatch")
    script.ResolveDefaultMethod() |> should equal (Some "defaults")

[<Test>]
let loadFScriptFlags() =
    let root = NUnit.Framework.TestContext.CurrentContext.TestDirectory
    let script = Terrabuild.Scripting.loadScript root [] "TestFiles/Extension.fss"
    let flags = script.TryGetFunctionFlags("run")
    flags |> should equal (Some [ ExportFlag.Cache Cacheability.Local ])

[<Test>]
let loadFScriptStringFlagsFails() =
    let root = NUnit.Framework.TestContext.CurrentContext.TestDirectory
    (fun () -> Terrabuild.Scripting.loadScript root [] "TestFiles/StringFlagsDescriptor.fss" |> ignore)
    |> should (throwWithMessage "Unsupported export flag for function 'dispatch'. Flags must be discriminated union cases") typeof<TerrabuildException>

[<Test>]
let loadFScriptBatchableFlagFails() =
    let root = NUnit.Framework.TestContext.CurrentContext.TestDirectory
    (fun () -> Terrabuild.Scripting.loadScript root [] "TestFiles/BatchableFlagDescriptor.fss" |> ignore)
    |> should (throwWithMessage "Unsupported export flag for function 'run'. Flags must be discriminated union cases") typeof<TerrabuildException>

[<Test>]
let invokeFScriptMethod() =
    let root = NUnit.Framework.TestContext.CurrentContext.TestDirectory
    let script = Terrabuild.Scripting.loadScript root [] "TestFiles/Extension.fss"
    let invocable = script.GetMethod("run")
    let context = { ActionContext.Debug = false
                    ActionContext.CI = false
                    ActionContext.Command = "run"
                    ActionContext.Hash = "abc"
                    ActionContext.Directory = "TestFiles"
                    ActionContext.Batch = None }
    let args = Value.Map (Map [ "context", Value.Object context ])
    let res = invocable.Value.Invoke<string> args
    res |> should equal "run"

[<Test>]
let invokeFScriptMethodCommandResult() =
    let root = NUnit.Framework.TestContext.CurrentContext.TestDirectory
    let script = Terrabuild.Scripting.loadScript root [] "TestFiles/CommandResult.fss"
    let invocable = script.GetMethod("run")
    let context = { ActionContext.Debug = false
                    ActionContext.CI = false
                    ActionContext.Command = "run"
                    ActionContext.Hash = "abc"
                    ActionContext.Directory = "TestFiles"
                    ActionContext.Batch = None }
    let args = Value.Map (Map [ "context", Value.Object context ])
    let res = invocable.Value.Invoke<CommandResult> args
    res.Batchable |> should equal true
    res.Operations.Length |> should equal 1
    res.Operations.Head.Command |> should equal "run"

[<Test>]
let invokeFScriptMethodWithStructuredArguments() =
    let root = NUnit.Framework.TestContext.CurrentContext.TestDirectory
    let script = Terrabuild.Scripting.loadScript root [] "TestFiles/PascalCaseDispatch.fss"
    let invocable = script.GetMethod("dispatch")
    let context = { ActionContext.Debug = false
                    ActionContext.CI = false
                    ActionContext.Command = "build"
                    ActionContext.Hash = "abc"
                    ActionContext.Directory = "TestFiles"
                    ActionContext.Batch = None }
    let args =
        Value.Map
            (Map [
                "context", Value.Object context
                "variables", Value.Map (Map [ "secret", Value.String "tagada" ])
                "args", Value.String "-n"
             ])

    let res = invocable.Value.Invoke<string> args
    res |> should equal "build|1|-n"

[<Test>]
let invokeFScriptMethodWithStructuredArgumentsDefaults() =
    let root = NUnit.Framework.TestContext.CurrentContext.TestDirectory
    let script = Terrabuild.Scripting.loadScript root [] "TestFiles/PascalCaseDispatch.fss"
    let invocable = script.GetMethod("dispatch")
    let context = { ActionContext.Debug = false
                    ActionContext.CI = false
                    ActionContext.Command = "build"
                    ActionContext.Hash = "abc"
                    ActionContext.Directory = "TestFiles"
                    ActionContext.Batch = None }
    let args =
        Value.Map
            (Map [
                "context", Value.Object context
             ])

    let res = invocable.Value.Invoke<string> args
    res |> should equal "build|0|<none>"

[<Test>]
let invokeFScriptMethodHasEnvInitialized() =
    let root = NUnit.Framework.TestContext.CurrentContext.TestDirectory
    let script = Terrabuild.Scripting.loadScript root [] "TestFiles/Env.fss"
    let invocable = script.GetMethod("run")
    let context = { ActionContext.Debug = false
                    ActionContext.CI = false
                    ActionContext.Command = "run"
                    ActionContext.Hash = "abc"
                    ActionContext.Directory = "TestFiles"
                    ActionContext.Batch = None }
    let args = Value.Map (Map [ "context", Value.Object context ])
    let res = invocable.Value.Invoke<string> args
    res |> should equal "Env.fss|0"

[<Test>]
let invokeFScriptMethodMissingContextFails() =
    let root = NUnit.Framework.TestContext.CurrentContext.TestDirectory
    let script = Terrabuild.Scripting.loadScript root [] "TestFiles/PascalCaseDispatch.fss"
    let invocable = script.GetMethod("dispatch")
    let args = Value.Map Map.empty
    (fun () -> invocable.Value.Invoke<string> args |> ignore)
    |> should (throwWithMessage "Missing required argument 'context' for function 'dispatch'") typeof<TerrabuildException>

[<Test>]
let invokeFScriptMethodMissingRequiredParameterFails() =
    let root = NUnit.Framework.TestContext.CurrentContext.TestDirectory
    let script = Terrabuild.Scripting.loadScript root [] "TestFiles/RequiredArgDispatch.fss"
    let invocable = script.GetMethod("dispatch")
    let context = { ActionContext.Debug = false
                    ActionContext.CI = false
                    ActionContext.Command = "build"
                    ActionContext.Hash = "abc"
                    ActionContext.Directory = "TestFiles"
                    ActionContext.Batch = None }
    let args = Value.Map (Map [ "context", Value.Object context ])
    (fun () -> invocable.Value.Invoke<string> args |> ignore)
    |> should (throwWithMessage "Missing required argument 'requiredArg' for function 'dispatch'") typeof<TerrabuildException>

let private withTemporaryWorkspace (test: string -> unit) =
    let tempRoot = Path.Combine(Path.GetTempPath(), $"terrabuild-scripting-tests-{Guid.NewGuid():N}")
    Directory.CreateDirectory(tempRoot) |> ignore
    try
        test tempRoot
    finally
        if Directory.Exists(tempRoot) then
            Directory.Delete(tempRoot, true)

[<Test>]
let fscriptSandboxExcludesRootGitForExistsAndKind() =
    withTemporaryWorkspace (fun root ->
        let gitDirectory = Path.Combine(root, ".git")
        Directory.CreateDirectory(gitDirectory) |> ignore
        File.WriteAllText(Path.Combine(gitDirectory, "config"), "test")

        let entryFile = Path.Combine(root, "Sandbox.fss")
        let source =
            """
[<export>] let inspect (context: {| Command: string |}) =
  let exists = Fs.exists ".git/config"
  let kind =
    match Fs.kind ".git/config" with
    | FsKind.Missing -> "missing"
    | FsKind.File _ -> "file"
    | FsKind.Directory _ -> "directory"
  $"{exists}|{kind}"

type ExportFlag =
  | Dispatch
  | Default
  | Never
  | Local
  | External
  | Remote

{ [nameof inspect] = [Remote] }
"""

        let script =
            Terrabuild.Scripting.loadScriptFromSourceWithIncludes
                root
                root
                entryFile
                source
                (fun _ -> None)

        let invocable = script.GetMethod("inspect")
        let context =
            { ActionContext.Debug = false
              ActionContext.CI = false
              ActionContext.Command = "inspect"
              ActionContext.Hash = "abc"
              ActionContext.Directory = root
              ActionContext.Batch = None }
        let args = Value.Map (Map [ "context", Value.Object context ])
        let result = invocable.Value.Invoke<string> args
        result |> should equal "false|missing")

[<Test>]
let fscriptSandboxExcludesRootGitForReadText() =
    withTemporaryWorkspace (fun root ->
        let gitDirectory = Path.Combine(root, ".git")
        Directory.CreateDirectory(gitDirectory) |> ignore
        File.WriteAllText(Path.Combine(gitDirectory, "config"), "test")

        let entryFile = Path.Combine(root, "SandboxRead.fss")
        let source =
            """
[<export>] let inspect (context: {| Command: string |}) =
  Fs.readText ".git/config"

type ExportFlag =
  | Dispatch
  | Default
  | Never
  | Local
  | External
  | Remote

{ [nameof inspect] = [Remote] }
"""

        let script =
            Terrabuild.Scripting.loadScriptFromSourceWithIncludes
                root
                root
                entryFile
                source
                (fun _ -> None)

        let invocable = script.GetMethod("inspect")
        let context =
            { ActionContext.Debug = false
              ActionContext.CI = false
              ActionContext.Command = "inspect"
              ActionContext.Hash = "abc"
              ActionContext.Directory = root
              ActionContext.Batch = None }
        let args = Value.Map (Map [ "context", Value.Object context ])

        (fun () -> invocable.Value.Invoke<string option> args |> ignore)
        |> should throw typeof<FScript.Language.EvalException>)

[<Test>]
let fscriptSandboxDeniesTraversalBypassToRootGit() =
    withTemporaryWorkspace (fun root ->
        let gitDirectory = Path.Combine(root, ".git")
        Directory.CreateDirectory(gitDirectory) |> ignore
        File.WriteAllText(Path.Combine(gitDirectory, "config"), "test")

        let entryFile = Path.Combine(root, "SandboxTraversal.fss")
        let source =
            """
[<export>] let inspect (context: {| Command: string |}) =
  Fs.readText ".git/../.git/config"

type ExportFlag =
  | Dispatch
  | Default
  | Never
  | Local
  | External
  | Remote

{ [nameof inspect] = [Remote] }
"""

        let script =
            Terrabuild.Scripting.loadScriptFromSourceWithIncludes
                root
                root
                entryFile
                source
                (fun _ -> None)

        let invocable = script.GetMethod("inspect")
        let context =
            { ActionContext.Debug = false
              ActionContext.CI = false
              ActionContext.Command = "inspect"
              ActionContext.Hash = "abc"
              ActionContext.Directory = root
              ActionContext.Batch = None }
        let args = Value.Map (Map [ "context", Value.Object context ])

        try
            invocable.Value.Invoke<string option> args |> ignore
            Assert.Fail("Expected denied path access error")
        with
        | :? FScript.Language.EvalException as exn ->
            exn.Message.Contains("denied path") |> should equal true)

[<Test>]
let fscriptSandboxSupportsCustomDeniedPathGlobs() =
    withTemporaryWorkspace (fun root ->
        let gitDirectory = Path.Combine(root, ".git")
        Directory.CreateDirectory(gitDirectory) |> ignore
        File.WriteAllText(Path.Combine(gitDirectory, "config"), "test")

        let modulesDirectory = Path.Combine(root, "project", "node_modules")
        Directory.CreateDirectory(modulesDirectory) |> ignore
        File.WriteAllText(Path.Combine(modulesDirectory, "pkg.json"), "{}")

        let entryFile = Path.Combine(root, "SandboxCustomDeny.fss")
        let source =
            """
[<export>] let inspect (context: {| Command: string |}) =
  let gitExists = Fs.exists ".git/config"
  let modulesExists = Fs.exists "project/node_modules/pkg.json"
  $"{gitExists}|{modulesExists}"

type ExportFlag =
  | Dispatch
  | Default
  | Never
  | Local
  | External
  | Remote

{ [nameof inspect] = [Remote] }
"""

        let script =
            Terrabuild.Scripting.loadScriptFromSourceWithIncludesWithDeniedPathGlobs
                root
                [ "**/node_modules" ]
                root
                entryFile
                source
                (fun _ -> None)

        let invocable = script.GetMethod("inspect")
        let context =
            { ActionContext.Debug = false
              ActionContext.CI = false
              ActionContext.Command = "inspect"
              ActionContext.Hash = "abc"
              ActionContext.Directory = root
              ActionContext.Batch = None }
        let args = Value.Map (Map [ "context", Value.Object context ])
        let result = invocable.Value.Invoke<string> args
        result |> should equal "true|false")
