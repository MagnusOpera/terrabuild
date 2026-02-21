module Extensions
open System
open System.IO
open System.Net.Http
open System.Security.Cryptography
open System.Text
open System.Reflection
open Terrabuild.Scripting
open Terrabuild.ScriptingContracts
open Terrabuild.Expressions
open Errors

type InvocationResult<'t> =
    | Success of 't
    | ScriptNotFound
    | TargetNotFound
    | ErrorTarget of Exception

let SystemExtensions = ScriptRegistry.SystemExtensions

let private extensionCandidates (name: string) =
    if String.IsNullOrWhiteSpace name then [ name ]
    elif name.StartsWith("@") then [ name; name.Substring(1) ]
    else [ name; $"@{name}" ]

let private embeddedScriptsVirtualRoot =
    Path.Combine(AppContext.BaseDirectory, "__terrabuild_embedded__", "Scripts")
    |> Path.GetFullPath

let private normalizeRelativeScriptPath (path: string) =
    path.Replace('\\', '/')

let private toEmbeddedScriptVirtualPath (relativeScriptPath: string) =
    let relative = normalizeRelativeScriptPath relativeScriptPath
    let prefix = "Scripts/"
    if relative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) then
        let localRelative = relative.Substring(prefix.Length)
        Path.Combine(embeddedScriptsVirtualRoot, localRelative) |> Path.GetFullPath
    else
        Path.Combine(embeddedScriptsVirtualRoot, relative) |> Path.GetFullPath

let private tryReadEmbeddedScript (assembly: Assembly) (relativeScriptPath: string) =
    let normalized = normalizeRelativeScriptPath relativeScriptPath
    let localRelative =
        if normalized.StartsWith("Scripts/", StringComparison.OrdinalIgnoreCase) then normalized.Substring("Scripts/".Length)
        else normalized

    let expectedSlashSuffix = ("scripts/" + localRelative).ToLowerInvariant()
    let expectedDotSuffix = ("scripts." + localRelative.Replace("/", ".")).ToLowerInvariant()
    let resourceName =
        assembly.GetManifestResourceNames()
        |> Array.tryFind (fun name ->
            let normalizedName = name.Replace('\\', '/').ToLowerInvariant()
            normalizedName.EndsWith(expectedSlashSuffix, StringComparison.Ordinal)
            || normalizedName.EndsWith(expectedDotSuffix, StringComparison.Ordinal))

    match resourceName with
    | Some resource ->
        match assembly.GetManifestResourceStream(resource) with
        | null -> None
        | stream ->
            use reader = new StreamReader(stream)
            reader.ReadToEnd() |> Some
    | None ->
        None

let private embeddedScriptSources =
    lazy
        (let assembly = Assembly.GetExecutingAssembly()
         ScriptRegistry.EmbeddedScriptFiles
         |> List.choose (fun relativeScriptPath ->
             let virtualPath = toEmbeddedScriptVirtualPath relativeScriptPath
             match tryReadEmbeddedScript assembly relativeScriptPath with
             | Some source -> Some (virtualPath, source)
             | None -> None)
         |> Map.ofList)

// NOTE: when app in package as a single file, Terrabuild.Assembly can't be found...
//       this means native deployments are not supported ¯\_(ツ)_/¯
let terrabuildDir : string =
    match Diagnostics.Process.GetCurrentProcess().MainModule with
    | NonNull mainModule -> mainModule.FileName |> FS.parentDirectory |> Option.get
    | _ -> raiseBugError "Unable to get the current process main module"

//  Diagnostics.Process.GetCurrentProcess().MainModule.FileName |> FS.parentDirectory
let terrabuildScripting =
    let path = FS.combinePath terrabuildDir "Terrabuild.Scripting.dll"
    path

let private httpClient = new HttpClient()

let private tryGetScriptUri (value: string) =
    try
        let uri = Uri(value, UriKind.Absolute)
        if uri.Scheme = Uri.UriSchemeHttp || uri.Scheme = Uri.UriSchemeHttps then Some uri
        else None
    with
    | :? UriFormatException -> None

let private isWithinWorkspace (workspaceRoot: string) (candidatePath: string) =
    let normalize (path: string) =
        let full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        if String.IsNullOrWhiteSpace full then full
        else full + string Path.DirectorySeparatorChar

    let workspace = normalize workspaceRoot
    let candidate = normalize candidatePath
    candidate.StartsWith(workspace, StringComparison.Ordinal)

let private resolveLocalScriptPath (workspaceRoot: string) (script: string) =
    let candidatePath =
        if Path.IsPathRooted script then script
        else Path.Combine(workspaceRoot, script)

    let resolved = Path.GetFullPath(candidatePath)
    if isWithinWorkspace workspaceRoot resolved |> not then
        raiseInvalidArg $"Script '{script}' is outside workspace '{workspaceRoot}'"
    resolved

let private urlHash (value: string) =
    let bytes = Encoding.UTF8.GetBytes(value)
    using (SHA256.Create()) (fun sha ->
        sha.ComputeHash(bytes)
        |> Array.map (fun b -> b.ToString("x2"))
        |> String.concat "")

let private downloadScript (workspaceRoot: string) (uri: Uri) =
    let extension =
        match Path.GetExtension(uri.AbsolutePath) with
        | null
        | "" -> ".fss"
        | ext -> ext

    let cacheDir = Path.Combine(workspaceRoot, ".terrabuild", ".scripts")
    Directory.CreateDirectory(cacheDir) |> ignore

    let hash = urlHash (uri.AbsoluteUri)
    let localFile = Path.Combine(cacheDir, $"{hash}{extension}")

    let response = httpClient.GetAsync(uri).GetAwaiter().GetResult()
    if response.IsSuccessStatusCode |> not then
        raiseExternalError $"Failed to download script from '{uri.AbsoluteUri}' (status {(int response.StatusCode)})"

    let content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    File.WriteAllText(localFile, content)
    localFile

let lazyLoadScript (workspaceRoot: string) (deniedPathGlobs: string list) (name: string) (script: string option) =
    let initScript () =
        match script with
        | Some script ->
            match tryGetScriptUri script with
            | Some uri ->
                if uri.Scheme <> Uri.UriSchemeHttps then
                    raiseInvalidArg $"Only HTTPS script URLs are allowed for extension '{name}'"
                let downloadedFile = downloadScript workspaceRoot uri
                loadScriptWithDeniedPathGlobs workspaceRoot [ terrabuildScripting ] deniedPathGlobs downloadedFile
            | _ ->
                let localScript = resolveLocalScriptPath workspaceRoot script
                loadScriptWithDeniedPathGlobs workspaceRoot [ terrabuildScripting ] deniedPathGlobs localScript
        | _ ->
            let systemScriptPath =
                extensionCandidates name
                |> List.tryPick (fun candidate -> ScriptRegistry.BuiltInScriptFiles |> Map.tryFind candidate)
            match systemScriptPath with
            | Some relativePath ->
                let entryPath = toEmbeddedScriptVirtualPath relativePath
                let sourceMap = embeddedScriptSources.Value
                match sourceMap |> Map.tryFind entryPath with
                | Some entrySource ->
                    let resolveImportedSource (path: string) =
                        sourceMap |> Map.tryFind (Path.GetFullPath(path))
                    loadScriptFromSourceWithIncludesWithDeniedPathGlobs
                        workspaceRoot
                        deniedPathGlobs
                        embeddedScriptsVirtualRoot
                        entryPath
                        entrySource
                        resolveImportedSource
                | None ->
                    raiseSymbolError $"Embedded script is not defined for extension '{name}'"
            | _ ->
                raiseSymbolError $"Script is not defined for extension '{name}'"

    lazy(initScript())

let getScript (extension: string) (Scripts: Map<string, Lazy<Script>>) =
    extensionCandidates extension
    |> List.tryPick (fun candidate -> Scripts |> Map.tryFind candidate)
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
