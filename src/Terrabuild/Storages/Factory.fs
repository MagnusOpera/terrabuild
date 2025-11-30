module Storages.Factory

let create api : Contracts.IStorage =
    match api with
    | None -> Local()
    | Some api -> AzureBlobStorage(api)
