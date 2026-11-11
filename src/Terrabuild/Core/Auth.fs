module Auth

open System
open System.IO
open System.Security.Cryptography

type SpaceAuth = {
    Token: string
    Key: string
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

let internal deriveKey workspaceId masterKey =
    let key = Encryption.masterKeyFromString workspaceId masterKey
    try Convert.ToBase64String(key)
    finally CryptographicOperations.ZeroMemory(key)

let internal decodeKey (value: string) =
    match Convert.FromBase64String(value) with
    | key when key.Length = 32 -> key
    | _ -> Errors.raiseInvalidArg "The stored workspace key is invalid. Import it again."

let readAuth workspaceId =
    let origin = Api.Endpoint.origin ()
    let store, _ = CredentialStore.publisher ()
    read store origin workspaceId

let login workspaceId token masterKey =
    let origin = Api.Endpoint.origin ()
    if String.IsNullOrWhiteSpace(masterKey) then
        Errors.raiseInvalidArg "The master key must not be empty."
    Api.Auth.loginSpace workspaceId token |> ignore
    let store, lockDirectory = CredentialStore.publisher ()
    save store lockDirectory origin workspaceId { Token = token; Key = deriveKey workspaceId masterKey }

let logout workspaceId =
    let store, lockDirectory = CredentialStore.publisher ()
    remove store lockDirectory workspaceId
