module Extensions
open System
open Terrabuild.Scripting
open Terrabuild.Expressions
open Errors
open Terrabuild.Configuration.AST
open Terrabuild.Extensibility

type InvocationResult<'t> =
    | Success of 't
    | ScriptNotFound
    | TargetNotFound
    | ErrorTarget of Exception

let systemExtensions =
    Terrabuild.Extensions.Factory.systemScripts
    |> Seq.map (fun kvp ->
        let defaults =
            match kvp.Key.ToLowerInvariant() with
            | "@shell" ->
                Some (Map [ "args", Expr.String "" ])
            | "@npx" ->
                Some (Map [ "args", Expr.String "" ])
            | _ ->
                None

        kvp.Key, { ExtensionBlock.Image = None
                   Platform = None
                   Variables = None
                   Script = None
                   Cpus = None
                   Defaults = defaults
                   Env = None })
    |> Map.ofSeq

let private extensionCandidates (name: string) =
    if String.IsNullOrWhiteSpace name then [ name ]
    elif name.StartsWith("@") then [ name; name.Substring(1) ]
    else [ name; $"@{name}" ]

// NOTE: when app in package as a single file, Terrabuild.Assembly can't be found...
//       this means native deployments are not supported ¯\_(ツ)_/¯
let terrabuildDir : string =
    match Diagnostics.Process.GetCurrentProcess().MainModule with
    | NonNull mainModule -> mainModule.FileName |> FS.parentDirectory |> Option.get
    | _ -> raiseBugError "Unable to get the current process main module"

//  Diagnostics.Process.GetCurrentProcess().MainModule.FileName |> FS.parentDirectory
let terrabuildExtensibility =
    let path = FS.combinePath terrabuildDir "Terrabuild.Extensibility.dll"
    path

let lazyLoadScript (workspaceRoot: string) (name: string) (script: string option) =
    let initScript () =
        match script with
        | Some script ->
            loadScript workspaceRoot [ terrabuildExtensibility ] script
        | _ ->
            let systemScriptPath =
                extensionCandidates name
                |> List.tryPick (fun candidate -> Terrabuild.Extensions.Factory.systemScriptFiles |> Map.tryFind candidate)
                |> Option.map (FS.combinePath terrabuildDir)

            match systemScriptPath with
            | Some scriptPath when System.IO.File.Exists scriptPath ->
                loadScript workspaceRoot [ terrabuildExtensibility ] scriptPath
            | _ ->
                let systemScript =
                    extensionCandidates name
                    |> List.tryPick (fun candidate -> Terrabuild.Extensions.Factory.systemScripts |> Map.tryFind candidate)

                match systemScript with
                | Some sysTpe -> Script(sysTpe)
                | _ -> raiseSymbolError $"Script is not defined for extension '{name}'"

    lazy(initScript())

let getScript (extension: string) (scripts: Map<string, Lazy<Script>>) =
    extensionCandidates extension
    |> List.tryPick (fun candidate -> scripts |> Map.tryFind candidate)
    |> Option.map _.Value

let invokeScriptMethod<'r> (method: string) (args: Value) (script: Script option) =
    match script with
    | None -> ScriptNotFound
    | Some script ->
        let resolveMethodName () =
            if String.Equals(method, "__defaults__", StringComparison.OrdinalIgnoreCase) then
                script.ResolveDefaultMethod()
            else
                script.ResolveCommandMethod(method)

        match resolveMethodName () with
        | None -> TargetNotFound
        | Some resolvedMethod ->
            let invocable = script.GetMethod(resolvedMethod)
            match invocable with
            | Some invocable ->
                try
                    Success (invocable.Invoke<'r> args)
                with
                | :? Reflection.TargetInvocationException as exn ->
                    match exn.InnerException with
                    | NonNull innerExn -> ErrorTarget innerExn
                    | _ -> ErrorTarget exn
                | exn -> ErrorTarget exn
            | None -> TargetNotFound

let private getScriptFlags (method: string) (script: Script option) =
    match script with
    | None -> None
    | Some script ->
        match script.ResolveCommandMethod(method) with
        | Some resolvedMethod -> script.TryGetFunctionFlags(resolvedMethod)
        | _ -> None

let getScriptCacheability (method: string) (script: Script option) =
    let cacheFlag =
        getScriptFlags method script
        |> Option.bind (List.tryPick (function | ExportFlag.Cache cacheability -> Some cacheability | _ -> None))
    cacheFlag

let isScriptBatchable (method: string) (script: Script option) =
    getScriptFlags method script
    |> Option.map (List.contains ExportFlag.Batchable)
    |> Option.defaultValue false
