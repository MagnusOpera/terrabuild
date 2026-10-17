module Terrabuild.Scripting.Scripting.Tests

open NUnit.Framework
open FsUnit
open Terrabuild.Scripting
open Terrabuild.ScriptingContracts
open Terrabuild.Expression
open Errors
open System
open System.IO
open System.Threading
open System.Threading.Tasks

[<Test>]
let preservationIsARequiredCommandOutputIncludingDispatchFallback() =
    let root = Path.GetTempPath()
    let source = """
type ExportFlag =
  | Remote
  | Never
  | Dispatch
type ProvenanceEffect = Preserved | Broken | Unaffected
type CommandResult = { Batchable: bool; Operations: string list; PreserveProvenance: ProvenanceEffect }
type LegacyCommandResult = { Batchable: bool; Operations: string list }
export let build context = { Batchable = false; Operations = []; PreserveProvenance = Preserved }
export let dispatch context = { Batchable = false; Operations = []; PreserveProvenance = Broken }
export let notify context = { Batchable = false; Operations = []; PreserveProvenance = Unaffected }
export let legacy context = { Batchable = false; Operations = [] }
{ [nameof build] = [Remote]
  [nameof dispatch] = [Dispatch; Never]
  [nameof legacy] = [Remote]
  [nameof notify] = [Remote] }
"""
    let script = loadScriptFromSourceWithIncludes root root (Path.Combine(root, "provenance-test.fss")) source (fun _ -> None)
    let context: ActionContext = { Debug = false; CI = false; Command = "build"; Hash = "test"; Directory = root; Batch = None }
    let arguments = Value.Map (Map ["context", Value.Object context])
    let invoke name = script.GetMethod(script.ResolveCommandMethod(name).Value).Value.Invoke<CommandResult> arguments
    (invoke "build").PreserveProvenance |> should equal Terrabuild.ScriptingContracts.ProvenanceEffect.Preserved
    (invoke "fallback").PreserveProvenance |> should equal Terrabuild.ScriptingContracts.ProvenanceEffect.Broken
    (fun () -> invoke "legacy" |> ignore) |> should throw typeof<TerrabuildException>
    (invoke "notify").PreserveProvenance |> should equal ProvenanceEffect.Unaffected

[<Test>]
let loadScript() =
    let root = NUnit.Framework.TestContext.CurrentContext.TestDirectory
    let script = Terrabuild.Scripting.loadScript root [] "TestFiles/Toto.fss"
    let invocable = script.GetMethod("tagada")
    let context = { ExtensionContext.Debug= false; ExtensionContext.Directory = "this is a path"; ExtensionContext.CI = false }
    let args = Value.Map (Map [ "context", Value.Object context])
    let res = invocable.Value.Invoke<string> args
    res |> should equal context.Directory

[<Test>]
let legacyScriptIsRejected() =
    let root = NUnit.Framework.TestContext.CurrentContext.TestDirectory
    let legacyScript = Path.Combine(root, "TestFiles", "Legacy.fsx")
    File.WriteAllText(legacyScript, "let value = 1")
    try
        (fun () -> Terrabuild.Scripting.loadScript root [] legacyScript |> ignore)
        |> should (throwWithMessage $"Legacy F# extension scripts are no longer supported; migrate '{legacyScript}' to '.fss'") typeof<TerrabuildException>
    finally
        if File.Exists(legacyScript) then
            File.Delete(legacyScript)

[<Test>]
let loadVSSolution() =
    let testDir = System.IO.Path.Combine(NUnit.Framework.TestContext.CurrentContext.TestDirectory, "TestFiles")
    let root = NUnit.Framework.TestContext.CurrentContext.TestDirectory
    let script = Terrabuild.Scripting.loadScript root [] "TestFiles/VSSolution.fss"
    let invocable = script.GetMethod("defaults")
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

let private concurrentLoadSource =
    """
import "shared.fss" as Shared

export let inspect (context: {| Command: string |}) = Shared.value

type ExportFlag =
  | Dispatch
  | Default
  | Never
  | Local
  | External
  | Remote

{ [nameof inspect] = [Remote] }
"""

[<Test>]
let distinctFScriptCacheMissesLoadConcurrently() =
    withTemporaryWorkspace (fun root ->
        use started = new CountdownEvent(2)

        let load entryName =
            Task.Run(fun () ->
                Terrabuild.Scripting.loadScriptFromSourceWithIncludes
                    root
                    root
                    (Path.Combine(root, entryName))
                    concurrentLoadSource
                    (fun _ ->
                        started.Signal() |> ignore
                        if not (started.Wait(TimeSpan.FromSeconds(5.0))) then
                            failwith "Distinct script preparations did not overlap"
                        Some "let value = \"ok\""))

        let first = load "ConcurrentA.fss"
        let second = load "ConcurrentB.fss"
        Task.WaitAll([| first :> Task; second :> Task |])
        first.Result.GetMethod("inspect").IsSome |> should equal true
        second.Result.GetMethod("inspect").IsSome |> should equal true)

[<Test>]
let failedFScriptCacheEntryCanBeRetried() =
    withTemporaryWorkspace (fun root ->
        let entryFile = Path.Combine(root, "Retry.fss")

        (fun () ->
            Terrabuild.Scripting.loadScriptFromSourceWithIncludes
                root
                root
                entryFile
                "let broken = missing"
                (fun _ -> None)
            |> ignore)
        |> should throw typeof<FScript.Language.TypeException>

        let script =
            Terrabuild.Scripting.loadScriptFromSourceWithIncludes
                root
                root
                entryFile
                concurrentLoadSource
                (fun _ -> Some "let value = \"ok\"")

        script.GetMethod("inspect").IsSome |> should equal true)

[<Test>]
let fscriptSandboxExcludesRootGitForExistsAndKind() =
    withTemporaryWorkspace (fun root ->
        let gitDirectory = Path.Combine(root, ".git")
        Directory.CreateDirectory(gitDirectory) |> ignore
        File.WriteAllText(Path.Combine(gitDirectory, "config"), "test")

        let entryFile = Path.Combine(root, "Sandbox.fss")
        let source =
            """
export let inspect (context: {| Command: string |}) =
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
export let inspect (context: {| Command: string |}) =
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
export let inspect (context: {| Command: string |}) =
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
export let inspect (context: {| Command: string |}) =
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

[<Test>]
let fscriptRegistryExcludesConsoleReadLine() =
    withTemporaryWorkspace (fun root ->
        let entryFile = Path.Combine(root, "ConsoleReadLineBlocked.fss")
        let source =
            """
export let inspect (context: {| Command: string |}) =
  Console.readLine ()

type ExportFlag =
  | Dispatch
  | Default
  | Never
  | Local
  | External
  | Remote

{ [nameof inspect] = [Remote] }
"""

        (fun () ->
            Terrabuild.Scripting.loadScriptFromSourceWithIncludes
                root
                root
                entryFile
                source
                (fun _ -> None)
            |> ignore)
        |> should throw typeof<FScript.Language.TypeException>)

[<Test>]
let fscriptRegistryExcludesTaskSpawnAndAwait() =
    withTemporaryWorkspace (fun root ->
        let entryFile = Path.Combine(root, "TaskBlocked.fss")
        let source =
            """
export let inspect (context: {| Command: string |}) =
  Task.await (Task.spawn (fun () -> "ok"))

type ExportFlag =
  | Dispatch
  | Default
  | Never
  | Local
  | External
  | Remote

{ [nameof inspect] = [Remote] }
"""

        (fun () ->
            Terrabuild.Scripting.loadScriptFromSourceWithIncludes
                root
                root
                entryFile
                source
                (fun _ -> None)
            |> ignore)
        |> should throw typeof<FScript.Language.TypeException>)

type MapKeyResult = { Number: int }

let private withMapKeyScript key result descriptor test =
    withTemporaryWorkspace (fun root ->
        let source =
            $"""
type Key = | First | Second
export let inspect (context: {{| Command: string |}}) = {result}
type ExportFlag = | Remote
{descriptor}
"""
        let script =
            Terrabuild.Scripting.loadScriptFromSourceWithIncludes
                root root (Path.Combine(root, $"MapKey-{key}.fss")) source (fun _ -> None)
        test script)

[<TestCase("1")>]
[<TestCase("true")>]
[<TestCase("First")>]
let nonStringDescriptorKeysAreRejected key =
    (fun () ->
        withMapKeyScript key "1" $"{{ [{key}] = [Remote] }}" ignore)
    |> should (throwWithMessage "Descriptor map keys must be strings") typeof<TerrabuildException>

[<TestCase("1")>]
[<TestCase("true")>]
[<TestCase("First")>]
let nonStringReturnedMapKeysAreRejected key =
    withMapKeyScript key $"{{ [{key}] = 1 }}" "{ [nameof inspect] = [Remote] }" (fun script ->
        let args = Value.Map (Map [ "context", Value.Map (Map [ "Command", Value.String "inspect" ]) ])
        (fun () -> script.GetMethod("inspect").Value.Invoke<Value> args |> ignore)
        |> should (throwWithMessage "Terrabuild map values expect string keys") typeof<TerrabuildException>
        (fun () -> script.GetMethod("inspect").Value.Invoke<MapKeyResult> args |> ignore)
        |> should (throwWithMessage "Record decoding expects string map keys") typeof<TerrabuildException>)

[<Test>]
let stringReturnedMapKeysRemainSupported () =
    withMapKeyScript "string" "{ [\"Number\"] = 42 }" "{ [nameof inspect] = [Remote] }" (fun script ->
        let args = Value.Map (Map [ "context", Value.Map (Map [ "Command", Value.String "inspect" ]) ])
        let method = script.GetMethod("inspect").Value
        method.Invoke<Value> args
        |> should equal (Value.Map (Map [ "Number", Value.Number 42 ]))
        let result = method.Invoke<MapKeyResult> args
        result.Number |> should equal 42)

[<TestCase("1")>]
[<TestCase("true")>]
[<TestCase("First")>]
let keyedMapsCanBeUsedInsideExtensions key =
    withMapKeyScript key $"{{ [{key}] = 42 }}[{key}]" "{ [nameof inspect] = [Remote] }" (fun script ->
        let args = Value.Map (Map [ "context", Value.Map (Map [ "Command", Value.String "inspect" ]) ])
        script.GetMethod("inspect").Value.Invoke<int option> args |> should equal (Some 42))

[<Test>]
let argumentsFollowDeclaredRecordOptionAndUnionTypes() =
    withTemporaryWorkspace (fun root ->
        let source = """
type Mode = Fast | Slow
type Inner = { Mode: Mode; Label: string option }
type Input = { Nested: Inner; Items: Inner list }
type ExportFlag = Remote
export let inspect (context: {| Command: string |}) (input: Input) (mode: Mode option) =
    let speed = match input.Nested.Mode with | Fast -> "fast" | Slow -> "slow"
    let optional = match mode with | Some Slow -> "slow" | _ -> "other"
    let label = Option.defaultValue "none" input.Nested.Label
    let count = List.length input.Items
    $"{context.Command}:{speed}:{optional}:{label}:{count}"
{ [nameof inspect] = [Remote] }
"""
        let script = loadScriptFromSourceWithIncludes root root (Path.Combine(root, "typed-arguments.fss")) source (fun _ -> None)
        let inner = Value.Map(Map ["Mode", Value.Enum "Fast"])
        let arguments = Value.Map(Map [
            "context", Value.Map(Map ["Command", Value.String "inspect"])
            "input", Value.Map(Map ["Nested", inner; "Items", Value.List [inner]])
            "mode", Value.Enum "Slow" ])
        script.GetMethod("inspect").Value.Invoke<string> arguments
        |> should equal "inspect:fast:slow:none:1")

[<Test>]
let mapParametersRemainMapsWhileNestedRecordValuesAreAdapted() =
    withTemporaryWorkspace (fun root ->
        let source = """
type Item = { Name: string }
type ExportFlag = Remote
export let inspect (context: {| Command: string |}) (items: Item map) =
    match items["first"] with | Some item -> item.Name | None -> "missing"
{ [nameof inspect] = [Remote] }
"""
        let script = loadScriptFromSourceWithIncludes root root (Path.Combine(root, "typed-map-arguments.fss")) source (fun _ -> None)
        let arguments = Value.Map(Map [
            "context", Value.Map(Map ["Command", Value.String "inspect"])
            "items", Value.Map(Map ["first", Value.Map(Map ["Name", Value.String "kept"])]) ])
        script.GetMethod("inspect").Value.Invoke<string> arguments |> should equal "kept")
