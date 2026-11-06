module Storages.Factory
open System
open Errors
open System.Collections.Concurrent
open Serilog


type internal RemoteStorage(
    api: Contracts.IApiClient,
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

    let locations = ConcurrentDictionary<string * string, Contracts.ArtifactLocation>()
    let missing = ConcurrentDictionary<string, DateTime>()
    let mutable batchSupported = true

    let location id operation =
        match locations.TryGetValue((id, operation)) with
        | true, cached when cached.ExpiresAt > DateTime.UtcNow.AddSeconds(10.0) ->
            Uri(cached.Uri), backend cached.Provider
        | _ ->
            let entry = id.LastIndexOf('/') |> fun index -> if index < 0 then id else id.Substring(0, index)
            match missing.TryGetValue(entry) with
            | true, expires when expires > DateTime.UtcNow && operation <> "put" ->
                raise (Contracts.ArtifactUnavailableException(id))
            | _ ->
                let uri, provider = api.GetArtifact id operation
                uri, backend provider

    interface Contracts.IBatchStorage with
        member _.PrepareReads entries =
            match api with
            | :? Contracts.IArtifactLookupApi as lookup when batchSupported ->
                try
                    for chunk in entries |> List.distinct |> List.chunkBySize 64 do
                        match (if batchSupported then lookup.LookupArtifacts chunk else None) with
                        | None -> batchSupported <- false
                        | Some results ->
                            let requested = Set.ofList chunk
                            for result in results do
                                if requested.Contains result.Entry then
                                    if not result.Found then
                                        missing[result.Entry] <- DateTime.UtcNow.AddSeconds(30.0)
                                    else
                                        missing.TryRemove(result.Entry) |> ignore
                                        for item in result.Locations do
                                            if item.Path.StartsWith(result.Entry + "/", StringComparison.Ordinal) &&
                                               (item.Operation = "get" || item.Operation = "head") then
                                                locations[(item.Path, item.Operation)] <- item
                with exn ->
                    Log.Warning(exn, "Batch artifact lookup failed; falling back to individual reads")
            | _ -> ()

    interface Contracts.IStorage with
        override _.Name = "Remote Artifact Storage"

        override _.Exists id =
            try
                let uri, storage = location id "head"
                storage.Exists id uri
            with :? Contracts.ArtifactUnavailableException -> false

        override _.TryDownload id =
            try
                let uri, storage = location id "get"
                storage.TryDownload id uri
            with :? Contracts.ArtifactUnavailableException -> None

        override _.Upload id summaryFile =
            let uri, storage = location id "put"
            storage.Upload id uri summaryFile
            let separator = id.LastIndexOf('/')
            if separator >= 0 then missing.TryRemove(id.Substring(0, separator)) |> ignore

let create api: Contracts.IStorage =
    match api with
    | None -> Local()
    | Some api -> RemoteStorage(api)
