module Auth

open System
open System.IO

type SpaceAuth = {
    Token: string
}

let private account workspaceId =
    "publisher/" + CredentialStore.identifier workspaceId

let private readAccounts (store: CredentialStore.Store) workspaceId =
    store.Read (account workspaceId)
    |> Option.map CredentialStore.deserialize<Map<string, SpaceAuth>>
    |> Option.defaultValue Map.empty

let internal read (store: CredentialStore.Store) (origin: Uri) workspaceId =
    readAccounts store workspaceId |> Map.tryFind origin.AbsoluteUri

let private lockAccount lockDirectory workspaceId =
    Directory.CreateDirectory(lockDirectory) |> ignore
    let lockFile = Path.Combine(lockDirectory, CredentialStore.identifier (account workspaceId) + ".lock")
    ExclusiveFileLock.acquire "workspace credential" lockFile

let internal save (store: CredentialStore.Store) lockDirectory (origin: Uri) workspaceId auth =
    use lease = lockAccount lockDirectory workspaceId
    readAccounts store workspaceId
    |> Map.add origin.AbsoluteUri auth
    |> Json.Serialize
    |> store.Write (account workspaceId)

let internal remove (store: CredentialStore.Store) lockDirectory workspaceId =
    use lease = lockAccount lockDirectory workspaceId
    store.Delete (account workspaceId)

let readAuth workspaceId =
    let origin = Api.Endpoint.origin ()
    let store, _ = CredentialStore.publisher ()
    read store origin workspaceId

let storeToken workspaceId token =
    let origin = Api.Endpoint.origin ()
    let store, lockDirectory = CredentialStore.publisher ()
    save store lockDirectory origin workspaceId { Token = token }

let logout workspaceId =
    let store, lockDirectory = CredentialStore.publisher ()
    remove store lockDirectory workspaceId
