module internal WorkspaceVault

open System
open System.Net.Http
open System.Security.Cryptography
open System.Text
open System.Text.Json
open Errors

let private authority (origin: Uri) = origin.GetLeftPart(UriPartial.Authority)

let path (workspace: string) suffix =
    "/terrabuild/v2/workspaces/" + Uri.EscapeDataString(workspace) + suffix

let request<'T> origin method route token body =
    use http = Insights.client ()
    let status, ok, data = Insights.request http origin method route token body
    if not ok then Insights.fail status data
    if typeof<'T> = typeof<unit> then Unchecked.defaultof<'T>
    else Json.Deserialize<'T>(data.GetRawText())

let readSecret label =
    if Console.IsInputRedirected then
        Console.ReadLine() |> Option.ofObj |> Option.defaultValue ""
    else
        eprintf "%s: " label
        let input = StringBuilder()
        let mutable doneReading = false
        while not doneReading do
            let key = Console.ReadKey(intercept = true)
            match key.Key with
            | ConsoleKey.Enter -> doneReading <- true
            | ConsoleKey.Backspace when input.Length > 0 -> input.Length <- input.Length - 1
            | _ when not (Char.IsControl key.KeyChar) -> input.Append(key.KeyChar) |> ignore
            | _ -> ()
        eprintfn ""
        input.ToString()

type Exchange = { AccessToken: string; KeyId: string }

let exchange origin workspace authentication =
    request<Exchange> origin HttpMethod.Post "/terrabuild/v2/auth/exchange" None
        (Some (Json.Serialize {| WorkspaceId = workspace; AuthCredential = authentication |}))

let loginCi workspace =
    let origin = Api.Endpoint.origin ()
    let token = readSecret "CI token"
    let authentication, keys = VaultCrypto.ciCredentials origin workspace token
    use _keys = keys
    exchange origin workspace authentication |> ignore
    Auth.storeToken workspace token

type AccountKey = { KeyId: string; WrappingKey: string; SigningKey: string; RecoveryBundle: string }
type Account = { UserId: string; Key: AccountKey option }

let private account origin session =
    request<Account> origin HttpMethod.Get "/terrabuild/v2/account" (Some session) None

let private verifyAccountKey (key: AccountKey) (keys: VaultCrypto.Keys) =
    if VaultCrypto.encode keys.WrappingPublic <> key.WrappingKey || VaultCrypto.encode keys.SigningPublic <> key.SigningKey then
        raiseInvalidArg "The unlocked account bundle does not match the registered identity."

let humanKeys origin session =
    let current = account origin session
    let key = current.Key |> Option.defaultWith (fun () -> raiseInvalidArg "Set up your account identity on the Insights website first.")
    let encoded = (CredentialStore.desktop ()).Read (AccountIdentity.slot origin current.UserId key.KeyId)
    let bundle = encoded |> Option.defaultWith (fun () -> raiseInvalidArg "Run terrabuild insights login to unlock your account identity on this machine.") |> VaultCrypto.decode
    try
        let keys = VaultCrypto.fromBundle bundle
        try
            verifyAccountKey key keys
            current.UserId, key.KeyId, keys
        with _ ->
            (keys :> IDisposable).Dispose()
            reraise()
    finally CryptographicOperations.ZeroMemory(bundle)

type Root = { RootId: string; Version: int; Status: string }
type Grant = { Context: string; Ciphertext: string; Signature: string }
type ReceivedGrant = {
    RootId: string
    Context: string
    Ciphertext: string
    Signature: string
    SigningKey: string
    ApproverKeyId: string
    ApproverUserId: string
}
type KeyState = { KeyId: string option; CanWrite: bool; Roots: Root list; Grants: ReceivedGrant list }
type Recipient = { KeyId: string; UserId: string option; CiTokenId: string option; WrappingKey: string; SigningKey: string }
type private Recipients = { Recipients: Recipient list; Approved: string list }

let getKeyState origin workspace session =
    request<KeyState> origin HttpMethod.Get (path workspace "/keys") (Some session) None

let unwrapRoot origin workspace kind identityId keyId (keys: VaultCrypto.Keys) (state: KeyState) (root: Root) =
    let grant = state.Grants |> List.tryFind (fun grant -> grant.RootId = root.RootId)
                |> Option.defaultWith (fun () -> raiseInvalidArg "Workspace access is pending key approval.")
    if state.KeyId <> Some keyId || not (VaultCrypto.verify grant.SigningKey grant.Signature (grant.Context + "\n" + grant.Ciphertext)) then
        raiseInvalidArg "Invalid workspace key grant signature."
    use document = JsonDocument.Parse(grant.Context)
    let values = document.RootElement.EnumerateArray() |> Seq.toArray
    if values.Length <> 11 then raiseInvalidArg "Invalid workspace key grant context."
    let field index = values[index].GetString()
    if field 0 <> "terrabuild-root-envelope-v1" || field 1 <> "x25519-xsalsa20-poly1305" ||
       field 2 <> authority origin || field 3 <> workspace || field 4 <> root.RootId ||
       values[5].GetInt32() <> root.Version || field 6 <> kind || field 7 <> identityId || field 8 <> keyId ||
       field 9 <> grant.ApproverUserId || field 10 <> grant.ApproverKeyId then
        raiseInvalidArg "Workspace key grant context does not match."
    let plaintext = VaultCrypto.unseal keys grant.Ciphertext
    try
        let context = VaultCrypto.utf8 grant.Context
        if plaintext.Length <> context.Length + 32 || not (plaintext.AsSpan(0, context.Length).SequenceEqual(context)) then
            raiseInvalidArg "Encrypted workspace key context does not match."
        plaintext[context.Length..]
    finally CryptographicOperations.ZeroMemory(plaintext)
