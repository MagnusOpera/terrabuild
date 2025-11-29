module Storages.Factory

let create api masterKey : Contracts.IStorage =
    match api with
    | None -> Local()
    | Some api -> AzureBlobStorage(api, masterKey)
