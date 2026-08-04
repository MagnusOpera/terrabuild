module Storages.Factory
open System
open Errors


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

    let location id operation =
        let uri, provider = api.GetArtifact id operation
        uri, backend provider

    interface Contracts.IStorage with
        override _.Name = "Remote Artifact Storage"

        override _.Exists id =
            let uri, storage = location id "head"
            storage.Exists id uri

        override _.TryDownload id =
            let uri, storage = location id "get"
            storage.TryDownload id uri

        override _.Upload id summaryFile =
            let uri, storage = location id "put"
            storage.Upload id uri summaryFile

let create api: Contracts.IStorage =
    match api with
    | None -> Local()
    | Some api -> RemoteStorage(api)
