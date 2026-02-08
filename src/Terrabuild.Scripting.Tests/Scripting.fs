module Terrabuild.Scripting.Scripting.Tests

open NUnit.Framework
open FsUnit
open Terrabuild.Extensibility
open Terrabuild.Expressions
open Errors


[<Test>]
let loadScript() =
    let script = Terrabuild.Scripting.loadScript [ "Terrabuild.Extensibility.dll" ] "TestFiles/Toto.fsx"
    let invocable = script.GetMethod("Tagada")
    let context = { ExtensionContext.Debug= false; ExtensionContext.Directory = "this is a path"; ExtensionContext.CI = false }
    let args = Value.Map (Map [ "context", Value.Object context])
    let res = invocable.Value.Invoke args
    res |> should equal context.Directory

[<Test>]
let loadScriptWithError() =
    (fun () -> Terrabuild.Scripting.loadScript [ "Terrabuild.Extensibility.dll" ] "TestFiles/Failure.fsx" |> ignore)
    |> should (throwWithMessage "Failed to identify function scope (either module or root class 'Failure')") typeof<TerrabuildException>

[<Test>]
let loadVSSolution() =
    let testDir = System.IO.Path.Combine(NUnit.Framework.TestContext.CurrentContext.TestDirectory, "TestFiles")
    let script = Terrabuild.Scripting.loadScript [ "Terrabuild.Extensibility.dll" ] "TestFiles/VSSolution.fsx"
    let invocable = script.GetMethod("__defaults__")
    let context = { ExtensionContext.Debug= false; ExtensionContext.Directory = testDir; ExtensionContext.CI = false }
    let args = Value.Map (Map [ "context", Value.Object context])

    let res = invocable.Value.Invoke<Terrabuild.Extensibility.ProjectInfo> args

    let expectedDependencies = Set [ "src"; "src\Terrabuild\Terrabuild.fsproj"; "src\Terrabuild.Configuration\Terrabuild.Configuration.fsproj" ]
    res.Dependencies |> should equal expectedDependencies

[<Test>]
let loadFScriptExports() =
    let script = Terrabuild.Scripting.loadScript [] "TestFiles/Extension.fss"
    script.GetMethod("run").IsSome |> should equal true
    script.GetMethod("hidden").IsNone |> should equal true

[<Test>]
let loadFScriptDispatchAndDefault() =
    let script = Terrabuild.Scripting.loadScript [] "TestFiles/Extension.fss"
    script.ResolveCommandMethod("unknown") |> should equal (Some "dispatch")
    script.ResolveDefaultMethod() |> should equal (Some "defaults")

[<Test>]
let loadFScriptFlags() =
    let script = Terrabuild.Scripting.loadScript [] "TestFiles/Extension.fss"
    let flags = script.TryGetFunctionFlags("run")
    flags |> should equal (Some [ ExportFlag.Batchable; ExportFlag.Cache Cacheability.Local ])

[<Test>]
let invokeFScriptMethod() =
    let script = Terrabuild.Scripting.loadScript [] "TestFiles/Extension.fss"
    let invocable = script.GetMethod("run")
    let context = { ActionContext.Debug = false
                    ActionContext.CI = false
                    ActionContext.Command = "run"
                    ActionContext.Hash = "abc"
                    ActionContext.Batch = None }
    let args = Value.Map (Map [ "context", Value.Object context ])
    let res = invocable.Value.Invoke<string> args
    res |> should equal "run"

[<Test>]
let invokeFScriptMethodWithStructuredArguments() =
    let script = Terrabuild.Scripting.loadScript [] "TestFiles/PascalCaseDispatch.fss"
    let invocable = script.GetMethod("dispatch")
    let context = { ActionContext.Debug = false
                    ActionContext.CI = false
                    ActionContext.Command = "build"
                    ActionContext.Hash = "abc"
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
    let script = Terrabuild.Scripting.loadScript [] "TestFiles/PascalCaseDispatch.fss"
    let invocable = script.GetMethod("dispatch")
    let context = { ActionContext.Debug = false
                    ActionContext.CI = false
                    ActionContext.Command = "build"
                    ActionContext.Hash = "abc"
                    ActionContext.Batch = None }
    let args =
        Value.Map
            (Map [
                "context", Value.Object context
             ])

    let res = invocable.Value.Invoke<string> args
    res |> should equal "build|0|<none>"

[<Test>]
let invokeFScriptMethodMissingContextFails() =
    let script = Terrabuild.Scripting.loadScript [] "TestFiles/PascalCaseDispatch.fss"
    let invocable = script.GetMethod("dispatch")
    let args = Value.Map Map.empty
    (fun () -> invocable.Value.Invoke<string> args |> ignore)
    |> should (throwWithMessage "Missing required argument 'context' for function 'dispatch'") typeof<TerrabuildException>

[<Test>]
let invokeFScriptMethodMissingRequiredParameterFails() =
    let script = Terrabuild.Scripting.loadScript [] "TestFiles/RequiredArgDispatch.fss"
    let invocable = script.GetMethod("dispatch")
    let context = { ActionContext.Debug = false
                    ActionContext.CI = false
                    ActionContext.Command = "build"
                    ActionContext.Hash = "abc"
                    ActionContext.Batch = None }
    let args = Value.Map (Map [ "context", Value.Object context ])
    (fun () -> invocable.Value.Invoke<string> args |> ignore)
    |> should (throwWithMessage "Missing required argument 'requiredArg' for function 'dispatch'") typeof<TerrabuildException>
