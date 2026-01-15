module EmbeddedUiFileProvider

open System
open System.IO
open System.Reflection
open System.Collections.Generic
open Microsoft.Extensions.FileProviders
open Microsoft.Extensions.Primitives

type private EmbeddedUiFileInfo(resourceName: string, name: string, assembly: Assembly) =
    interface IFileInfo with
        member _.Exists = true
        member _.Length =
            try
                use stream = assembly.GetManifestResourceStream(resourceName)
                if isNull stream then -1L else stream.Length
            with _ -> -1L
        member _.PhysicalPath = null
        member _.Name = name
        member _.LastModified = DateTimeOffset.MinValue
        member _.IsDirectory = false
        member _.CreateReadStream() =
            match assembly.GetManifestResourceStream(resourceName) with
            | null -> raise (FileNotFoundException($"Embedded resource not found: {resourceName}"))
            | stream -> stream

type Provider(assembly: Assembly) =
    let normalizePath (path: string) =
        path.TrimStart('/').Replace('\\', '/')

    let addKey (map: Dictionary<string, string>) key value =
        if map.ContainsKey(key) |> not then
            map.Add(key, value)

    let resourceMap =
        let map = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        let names = assembly.GetManifestResourceNames()
        for name in names do
            let normalized = name.Replace('\\', '/')
            if normalized.StartsWith("ui/") then
                let rel = normalized.Substring(3)
                addKey map rel name
            elif normalized.StartsWith("ui.") then
                let rel = normalized.Substring(3)
                addKey map rel name
                addKey map (rel.Replace('.', '/')) name
            elif normalized.Contains(".ui.") then
                let idx = normalized.IndexOf(".ui.", StringComparison.OrdinalIgnoreCase)
                let rel = normalized.Substring(idx + 4)
                addKey map rel name
                addKey map (rel.Replace('.', '/')) name
            elif normalized.Contains("/ui/") then
                let idx = normalized.IndexOf("/ui/", StringComparison.OrdinalIgnoreCase)
                let rel = normalized.Substring(idx + 4)
                addKey map rel name
        map

    interface IFileProvider with
        member _.GetFileInfo(subpath) =
            let key = normalizePath subpath
            match resourceMap.TryGetValue(key) with
            | true, resourceName ->
                EmbeddedUiFileInfo(resourceName, Path.GetFileName(key), assembly) :> IFileInfo
            | _ -> NotFoundFileInfo(subpath) :> IFileInfo

        member _.GetDirectoryContents(_subpath) =
            NotFoundDirectoryContents.Singleton :> IDirectoryContents

        member _.Watch(_filter) =
            NullChangeToken.Singleton
