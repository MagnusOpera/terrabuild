
module TestHelpers
open Terrabuild.Extensibility

open System.Reflection

let getCacheInfo<'T> name =
    match typeof<'T>.GetMethod(name, BindingFlags.Public ||| BindingFlags.Static) with
    | NonNull methodInfo ->
        match methodInfo.GetCustomAttribute(typeof<CacheableAttribute>) with
        | :? CacheableAttribute as attr -> attr.Cacheability
        | _ -> failwithf "Failed to get CacheableAttribute"
    | _ -> failwithf "expression is not a method"

let getBatchInfo<'T> name =
    match typeof<'T>.GetMethod(name, BindingFlags.Public ||| BindingFlags.Static) with
    | NonNull methodInfo ->
        match methodInfo.GetCustomAttribute(typeof<BatchableAttribute>) with
        | :? BatchableAttribute -> true
        | _ -> false
    | _ -> failwithf "expression is not a method"

let someArgs = Some "--opt1 --opt2"
let noneArgs = None

let normalize (ops: Terrabuild.Extensibility.ShellOperations) =
    ops |> List.map (fun op -> 
        { op with Arguments = op.Arguments |> String.normalizeShellArgs })


let ciContext =
    { ActionContext.Debug = true
      ActionContext.CI = true
      ActionContext.Command = "ci-command"
      ActionContext.Hash = "ABCDEF123456789"
      ActionContext.Batch = None }

let localContext =
    { ActionContext.Debug = false
      ActionContext.CI = false
      ActionContext.Command = "local-command"
      ActionContext.Hash = "123456789ABCDEF"
      ActionContext.Batch = None }

let batchContext tmpDir projectDirs =
    { ActionContext.Debug = false
      ActionContext.CI = false
      ActionContext.Command = "local-command"
      ActionContext.Hash = "123456789ABCDEF"
      ActionContext.Batch = Some { BatchContext.Hash = "FEDCBA987654321"
                                   BatchContext.TempDir = tmpDir
                                   BatchContext.ProjectPaths = projectDirs } }


let someMap = [ "prm1", "val1"
                "prm2", "val2" ] |> Map |> Some

let noneMap = None
