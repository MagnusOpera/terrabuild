module Storages.Factory
open System
open Errors
open System.Collections.Concurrent
open Serilog


type internal RemoteStorage(
    lookupArtifacts: string list -> Contracts.ArtifactLookup list,
    getUploadLocation: (string -> Uri * string option) option,
    ?azureBackend: IRemoteStorageBackend,
    ?cloudflareBackend: IRemoteStorageBackend
) =
    let azure = defaultArg azureBackend (AzureBlobStorage())
    let cloudflare = defaultArg cloudflareBackend (CloudflareR2())

    let backend (provider: string option) =
        match provider with
        | None -> azure
        | Some value when String.Equals(value, "r2", StringComparison.OrdinalIgnoreCase) -> cloudflare
        | Some value -> raiseExternalError $"Unsupported artifact storage provider '{value}'."

    let lookups = ConcurrentDictionary<string, DateTime * Contracts.ArtifactLookup>()

    let entryOf (path: string) =
        let separator = path.LastIndexOf('/')
        if separator < 0 then path else path.Substring(0, separator)

    let lookup entries =
        let results = lookupArtifacts entries |> Seq.map (fun result -> result.Entry, result) |> Map.ofSeq
        for entry in entries do
            match results |> Map.tryFind entry with
            | Some result ->
                let refreshAt =
                    match result.Locations with
                    | [] -> DateTime.UtcNow.AddSeconds(30.0)
                    | locations -> (locations |> List.minBy _.ExpiresAt).ExpiresAt.AddSeconds(-10.0)
                lookups[entry] <- refreshAt, result
            | None -> raiseExternalError $"Insights omitted artifact entry '{entry}' from the lookup response."

    let readLocation path operation =
        let entry = entryOf path
        let find (result: Contracts.ArtifactLookup) =
            if result.Found then
                result.Locations
                |> List.tryFind (fun item ->
                    item.Path = path && item.Operation = operation && item.ExpiresAt > DateTime.UtcNow)
            else None
        let refresh () =
            lookup [ entry ]
            match lookups.TryGetValue(entry) with
            | true, (_, result) -> find result
            | _ -> None
        let location =
            match lookups.TryGetValue(entry) with
            | true, (refreshAt, result) when refreshAt > DateTime.UtcNow ->
                match find result with
                | None when result.Found ->
                    // A manifest can point to a generation published after prefetch.
                    refresh ()
                | location -> location
            | _ -> refresh ()
        location |> Option.map (fun item -> Uri(item.Uri), backend item.Provider)

    new(api: Contracts.IApiClient, ?azureBackend: IRemoteStorageBackend, ?cloudflareBackend: IRemoteStorageBackend) =
        RemoteStorage(api.LookupArtifacts, Some api.GetUploadLocation, ?azureBackend = azureBackend, ?cloudflareBackend = cloudflareBackend)

    interface Contracts.IBatchStorage with
        member _.PrepareReads entries =
            for chunk in entries |> List.distinct |> List.chunkBySize 64 do
                try lookup chunk
                with exn ->
                    Log.Warning(exn, "Artifact lookup prefetch failed; retrying on demand")

    interface Contracts.IStorage with
        override _.Name = "Remote Artifact Storage"

        override _.Exists id =
            match readLocation id "head" with
            | Some (uri, storage) -> storage.Exists id uri
            | None -> false

        override _.TryDownload id =
            match readLocation id "get" with
            | Some (uri, storage) -> storage.TryDownload id uri
            | None -> None

        override _.Upload id summaryFile =
            match getUploadLocation with
            | None -> raiseBugError "Upload attempted with read-only artifact storage."
            | Some locate ->
                let uri, provider = locate id
                (backend provider).Upload id uri summaryFile
                lookups.TryRemove(entryOf id) |> ignore

let create api: Contracts.IStorage =
    match api with
    | None -> Local()
    | Some api -> RemoteStorage(api)


let createReadOnly lookupArtifacts: Contracts.IStorage =
    RemoteStorage(lookupArtifacts, None)
