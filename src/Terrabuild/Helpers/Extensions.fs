module Extensions
open System
open System.IO
open System.Net.Http
open System.Security.Cryptography
open System.Text
open System.Text.RegularExpressions
open System.Reflection
open Microsoft.Extensions.FileSystemGlobbing
open Terrabuild.Scripting
open Terrabuild.ScriptingContracts
open Terrabuild.Expression
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

type private ScriptOrigin =
    | LocalFile of fullPath: string
    | RemoteUrl of uri: Uri

let private tryGetScriptUri (value: string) =
    try
        let uri = Uri(value, UriKind.Absolute)
        if uri.Scheme = Uri.UriSchemeHttp || uri.Scheme = Uri.UriSchemeHttps then Some uri
        else None
    with
    | :? UriFormatException -> None

let private ensureHttpsUri (extensionName: string) (uri: Uri) =
    if uri.Scheme <> Uri.UriSchemeHttps then
        raiseInvalidArg $"Only HTTPS script URLs are allowed for extension '{extensionName}'"
    uri

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

let private hasWildcardCharacters (pattern: string) =
    pattern.IndexOfAny([| '*'; '?'; '['; ']'; '!' |]) >= 0

let private normalizePathForMatching (value: string) =
    value.Replace('\\', '/').TrimStart('/')

let private matchesDeniedGlob (relativePath: string) (glob: string) =
    let relativePath = normalizePathForMatching relativePath
    let glob = glob.Trim() |> normalizePathForMatching
    if String.IsNullOrWhiteSpace glob then
        false
    elif hasWildcardCharacters glob then
        let matcher = Matcher()
        matcher.AddInclude(glob) |> ignore
        matcher.Match(relativePath).HasMatches
    else
        relativePath = glob
        || relativePath.StartsWith($"{glob}/", StringComparison.Ordinal)
        || relativePath.Contains($"/{glob}/", StringComparison.Ordinal)
        || relativePath.EndsWith($"/{glob}", StringComparison.Ordinal)

let private isDeniedWorkspacePath (workspaceRoot: string) (deniedPathGlobs: string list) (path: string) =
    let relative = FS.relativePath workspaceRoot path |> normalizePathForMatching
    deniedPathGlobs |> List.exists (matchesDeniedGlob relative)

let private resolveImportedFilePath
    (workspaceRoot: string)
    (deniedPathGlobs: string list)
    (importerPath: string)
    (importPath: string) =
    let importerDir =
        match Path.GetDirectoryName(importerPath) with
        | NonNull value -> value
        | Null -> raiseInvalidArg $"Unable to resolve importer directory for '{importerPath}'"
    let candidatePath =
        if Path.IsPathRooted importPath then importPath
        else Path.Combine(importerDir, importPath)
    let resolved = Path.GetFullPath(candidatePath)
    if isWithinWorkspace workspaceRoot resolved |> not then
        raiseInvalidArg $"Script import '{importPath}' from '{importerPath}' is outside workspace '{workspaceRoot}'"
    if isDeniedWorkspacePath workspaceRoot deniedPathGlobs resolved then
        raiseInvalidArg $"Script import '{importPath}' from '{importerPath}' resolves to denied path '{resolved}'"
    resolved

let private urlHash (value: string) =
    let bytes = Encoding.UTF8.GetBytes(value)
    using (SHA256.Create()) (fun sha ->
        sha.ComputeHash(bytes)
        |> Array.map (fun b -> b.ToString("x2"))
        |> String.concat "")

let private downloadScriptSource (uri: Uri) =
    let response = httpClient.GetAsync(uri).GetAwaiter().GetResult()
    if response.IsSuccessStatusCode |> not then
        raiseExternalError $"Failed to download script from '{uri.AbsoluteUri}' (status {(int response.StatusCode)})"
    response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

let private sanitizePathSegment (value: string) =
    let invalidChars = Path.GetInvalidFileNameChars() |> Set.ofArray
    value
    |> Seq.map (fun ch -> if invalidChars.Contains(ch) then '_' else ch)
    |> Seq.toArray
    |> String

let private toUrlVirtualPath (workspaceRoot: string) (uri: Uri) =
    let cacheRoot = Path.Combine(workspaceRoot, ".terrabuild", ".scripts", "url")
    let scheme = sanitizePathSegment uri.Scheme
    let host =
        (if uri.IsDefaultPort then uri.Host
         else $"{uri.Host}_{uri.Port}")
        |> sanitizePathSegment
    let segments =
        uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries)
        |> Array.map sanitizePathSegment
        |> List.ofArray
    let fileName, parents =
        match segments |> List.rev with
        | [] -> "index.fss", []
        | head :: tail ->
            let baseName =
                if String.IsNullOrWhiteSpace(Path.GetExtension(head)) then $"{head}.fss"
                else head
            let withQuery =
                if String.IsNullOrWhiteSpace(uri.Query) then baseName
                else
                    let ext = Path.GetExtension(baseName)
                    let stem = Path.GetFileNameWithoutExtension(baseName)
                    $"{stem}_{urlHash uri.Query}{ext}"
            withQuery, (tail |> List.rev)
    [ cacheRoot; scheme; host ]
    |> List.append parents
    |> List.append [ fileName ]
    |> List.toArray
    |> Path.Combine
    |> Path.GetFullPath

let private importLineRegex =
    Regex("^([ \\t]*import[ \\t]+\")([^\"]+)(\"[ \\t]+as[ \\t]+[A-Za-z_][A-Za-z0-9_]*.*)$", RegexOptions.Compiled)

let private rewriteScriptImports (source: string) (rewriteImportPath: string -> string) =
    let newline =
        if source.Contains("\r\n", StringComparison.Ordinal) then "\r\n"
        else "\n"
    source
    |> fun text -> text.Replace("\r\n", "\n", StringComparison.Ordinal)
    |> fun text -> text.Split('\n')
    |> Array.map (fun line ->
        let m = importLineRegex.Match(line)
        if m.Success then
            let importPath = m.Groups[2].Value
            let rewritten = rewriteImportPath importPath
            $"{m.Groups[1].Value}{rewritten}{m.Groups[3].Value}"
        else
            line)
    |> String.concat newline

let private relativeImportPath (fromFile: string) (targetFile: string) =
    let fromDir =
        match Path.GetDirectoryName(fromFile) with
        | NonNull value -> value
        | Null -> raiseInvalidArg $"Unable to resolve parent directory for script '{fromFile}'"
    let relative = Path.GetRelativePath(fromDir, targetFile).Replace('\\', '/')
    if relative.StartsWith(".", StringComparison.Ordinal) then relative
    else $"./{relative}"

let private originKey = function
    | LocalFile path -> $"file:{path}"
    | RemoteUrl uri -> $"url:{uri.AbsoluteUri}"

let private loadScriptFromOriginWithIncludes
    (workspaceRoot: string)
    (deniedPathGlobs: string list)
    (extensionName: string)
    (entryOrigin: ScriptOrigin) =
    let sourceMap = Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
    let loadedOrigins = Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
    let loadingOrigins = Collections.Generic.HashSet<string>(StringComparer.Ordinal)
    let remoteSourceCache = Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)

    let rec ensureLoaded (origin: ScriptOrigin) =
        let key = originKey origin
        match loadedOrigins.TryGetValue(key) with
        | true, value -> value
        | _ ->
            if loadingOrigins.Contains(key) then
                raiseInvalidArg $"Detected circular script import while loading '{key}'"
            loadingOrigins.Add(key) |> ignore

            let virtualPath, rawSource =
                match origin with
                | LocalFile path ->
                    path, File.ReadAllText(path)
                | RemoteUrl uri ->
                    let virtualPath = toUrlVirtualPath workspaceRoot uri
                    let source =
                        match remoteSourceCache.TryGetValue(uri.AbsoluteUri) with
                        | true, cached -> cached
                        | _ ->
                            let downloaded = downloadScriptSource uri
                            remoteSourceCache[uri.AbsoluteUri] <- downloaded
                            downloaded
                    virtualPath, source

            let rewrittenSource =
                rewriteScriptImports rawSource (fun importPath ->
                    let importedOrigin =
                        match tryGetScriptUri importPath with
                        | Some uri ->
                            let httpsUri = ensureHttpsUri extensionName uri
                            RemoteUrl httpsUri
                        | None ->
                            match origin with
                            | LocalFile importerPath ->
                                let resolved = resolveImportedFilePath workspaceRoot deniedPathGlobs importerPath importPath
                                LocalFile resolved
                            | RemoteUrl importerUri ->
                                let resolvedUri = Uri(importerUri, importPath) |> ensureHttpsUri extensionName
                                RemoteUrl resolvedUri

                    let importedVirtualPath = ensureLoaded importedOrigin
                    relativeImportPath virtualPath importedVirtualPath)

            let fullVirtualPath = Path.GetFullPath(virtualPath)
            sourceMap[fullVirtualPath] <- rewrittenSource
            loadedOrigins[key] <- fullVirtualPath
            loadingOrigins.Remove(key) |> ignore
            fullVirtualPath

    let entryPath = ensureLoaded entryOrigin
    let entrySource = sourceMap[entryPath]
    let resolveImportedSource (path: string) =
        let fullPath = Path.GetFullPath(path)
        match sourceMap.TryGetValue(fullPath) with
        | true, source -> Some source
        | _ -> None

    loadScriptFromSourceWithIncludesWithDeniedPathGlobs
        workspaceRoot
        deniedPathGlobs
        workspaceRoot
        entryPath
        entrySource
        resolveImportedSource

let lazyLoadScript (workspaceRoot: string) (deniedPathGlobs: string list) (name: string) (script: string option) =
    let initScript () =
        match script with
        | Some script ->
            if ScriptRegistry.BuiltInScriptFiles |> Map.containsKey name then
                raiseInvalidArg $"Script override is not allowed for built-in extension '{name}'"
            match tryGetScriptUri script with
            | Some uri ->
                let uri = ensureHttpsUri name uri
                loadScriptFromOriginWithIncludes workspaceRoot deniedPathGlobs name (RemoteUrl uri)
            | _ ->
                let localScript = resolveLocalScriptPath workspaceRoot script
                if isDeniedWorkspacePath workspaceRoot deniedPathGlobs localScript then
                    raiseInvalidArg $"Script '{script}' resolves to denied path '{localScript}'"
                loadScriptFromOriginWithIncludes workspaceRoot deniedPathGlobs name (LocalFile localScript)
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
