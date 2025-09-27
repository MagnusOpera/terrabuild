module Extensions
open System
open Terrabuild.Scripting
open Terrabuild.Expressions
open Errors
open Terrabuild.Configuration.AST

type InvocationResult<'t> =
    | Success of 't
    | ScriptNotFound
    | TargetNotFound
    | ErrorTarget of Exception

let systemExtensions =
    Terrabuild.Extensions.Factory.systemScripts
    |> Seq.map (fun kvp ->
        kvp.Key, { ExtensionBlock.Container = None
                   Platform = None
                   Variables = None
                   Script = None
                   Defaults = None
                   Batch = None })
    |> Map.ofSeq

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

let lazyLoadScript (name: string) (script: string option) =
    let initScript () =
        match script with
        | Some script ->
            loadScript [ terrabuildExtensibility ] script
        | _ ->
            match Terrabuild.Extensions.Factory.systemScripts |> Map.tryFind name with
            | Some sysTpe -> Script(sysTpe)
            | _ -> raiseSymbolError $"Script is not defined for extension '{name}'"

    lazy(initScript())

let getScript (extension: string) (scripts: Map<string, Lazy<Script>>) =
    scripts
    |> Map.tryFind extension
    |> Option.map (fun script -> script.Value)

let invokeScriptMethod<'r> (method: string) (args: Value) (script: Script option) =
    match script with
    | None -> ScriptNotFound
    | Some script ->
        let rec invokeScriptMethod (method: string) =
            let invocable = script.GetMethod(method)
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
            | None ->
                match method with
                | method when method.StartsWith("__") -> TargetNotFound
                | _ -> invokeScriptMethod "__dispatch__"

        invokeScriptMethod method

let getScriptAttribute<'a when 'a :> Attribute> (method: string) (script: Script option) =
    match script with
    | None -> None
    | Some script ->
        match script.GetAttribute<'a> method with
        | Some attr -> Some attr
        | _ -> 
            match method with
            | method when method.StartsWith("__") -> None
            | _ -> script.GetAttribute<'a> "__dispatch__"
