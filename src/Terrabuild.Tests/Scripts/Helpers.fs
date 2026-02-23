module Terrabuild.Tests.Scripts.Helpers

open System
open System.IO
open Terrabuild
open Terrabuild.Expression
open Terrabuild.ScriptingContracts
open Errors

let private failInvoke which =
    function
    | Extensions.Success value -> value
    | Extensions.ScriptNotFound -> failwithf "Script not found for %s" which
    | Extensions.TargetNotFound -> failwithf "Target not found for %s" which
    | Extensions.ErrorTarget ex -> raise ex

let private workspaceRoot = NUnit.Framework.TestContext.CurrentContext.TestDirectory
let private denied = [ ".git" ]

let script name =
    Extensions.lazyLoadScript workspaceRoot denied name None
    |> fun loader -> loader.Value

let fixtureDir relativePath =
    Path.Combine(workspaceRoot, "TestFiles", "Scripts", relativePath)

let localContext command directory =
    { ActionContext.Debug = false
      ActionContext.CI = false
      ActionContext.Command = command
      ActionContext.Hash = "123456"
      ActionContext.Directory = directory
      ActionContext.Batch = None }

let ciContext command directory =
    { ActionContext.Debug = false
      ActionContext.CI = true
      ActionContext.Command = command
      ActionContext.Hash = "ABCDEF"
      ActionContext.Directory = directory
      ActionContext.Batch = None }

let batchContext command directory tempDir projectPaths =
    Directory.CreateDirectory(tempDir) |> ignore
    { ActionContext.Debug = false
      ActionContext.CI = false
      ActionContext.Command = command
      ActionContext.Hash = "123456"
      ActionContext.Directory = directory
      ActionContext.Batch =
        Some
            { BatchContext.Hash = "FEDCBA"
              BatchContext.TempDir = tempDir
              BatchContext.ProjectPaths = projectPaths |> Set.ofList
              BatchContext.BatchCommands = [] } }

let private withContext context args =
    args
    |> Map.add "context" (Value.Object context)
    |> Value.Map

let str value = Value.String value
let bool value = Value.Bool value
let int value = Value.Number value
let map (value: (string * Value) list) = Value.Map(Map.ofList value)
let list values = Value.List values

let invokeResult extensionName method context (args: Map<string, Value>) =
    Extensions.invokeScriptMethod<CommandResult> method (withContext context args) (script extensionName |> Some)
    |> failInvoke $"{extensionName}:{method}"

let invokeDefaults extensionName context =
    Extensions.invokeScriptMethod<ProjectInfo> "__defaults__" (withContext context Map.empty) (script extensionName |> Some)
    |> failInvoke $"{extensionName}:defaults"

let cacheability extensionName method =
    Extensions.getScriptCacheability method (script extensionName |> Some)

let normalizeOps (ops: ShellOperations) =
    ops
    |> List.map (fun op ->
        { op with Arguments = String.normalizeShellArgs op.Arguments })

let op command arguments errorLevel =
    { ShellOperation.Command = command
      ShellOperation.Arguments = arguments
      ShellOperation.ErrorLevel = errorLevel }
