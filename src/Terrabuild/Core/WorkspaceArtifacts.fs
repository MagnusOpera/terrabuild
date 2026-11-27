module internal WorkspaceArtifacts

open System
open System.Collections.Concurrent
open System.IO
open System.Net
open System.Net.Http
open System.Security.Cryptography
open System.Text.Json
open Errors

type private Repository = { RepositoryId: string; UserId: string; CiTokenId: string option }
type private FileRecord = { Kind: string; Sha256: string; Size: int64 }

type private Manifest = {
    Version: int
    WorkspaceId: string
    BuildId: string
    RepositoryId: string
    Entry: string
    Generation: string
    ProductionId: string
    RootId: string
    RootVersion: int
    PublisherKeyId: string
    Successful: bool
    Files: FileRecord list
}

type private Location = { Kind: string; Uri: string; ExpiresAt: DateTimeOffset; Headers: Map<string, string> option }
type private Accepted = {
    Entry: string
    Generation: string
    PublisherKeyId: string
    Digest: string
    Manifest: string
    Signature: string
    SigningKey: string
    AcceptedAt: DateTimeOffset
    Locations: Location list
}

type private Lookup = { Entries: Accepted list }
type private Publication = { Accepted: bool; Digest: string option; Locations: Location list option }

let private mapping origin workspace repositoryName =
    let name = CredentialStore.identifier (String.concat "\n" [ origin; workspace; repositoryName ])
    Path.Combine(Cache.createTerrabuildProfile(), "insights-repositories", name)

let private scope origin workspace repository =
    CredentialStore.identifier (String.concat "\n" [ origin; workspace; repository ])

let cachedScope (origin: Uri) workspace repositoryName =
    let origin = origin.GetLeftPart(UriPartial.Authority)
    let file = mapping origin workspace repositoryName
    try
        let id = IO.readTextFile file
        if id.Length > 0 && id |> Seq.forall Char.IsAsciiDigit then Some (scope origin workspace id)
        else None
    with :? IOException -> None

let private splitPath (path: string) =
    let index = path.LastIndexOf('/')
    if index < 0 then raiseInvalidArg "Invalid workspace cache path."
    path[..index - 1], path[index + 1..]

let private context (manifest: Manifest) kind =
    [| box manifest.Version; box manifest.WorkspaceId; box manifest.RepositoryId; box manifest.Entry
       box manifest.RootId; box manifest.RootVersion; box manifest.Generation; box kind |]
    |> JsonSerializer.Serialize

let private getUri (location: Location) =
    let uri = Uri(location.Uri)
    if uri.Scheme <> "https" && not (uri.Scheme = "http" && uri.IsLoopback) then
        raiseInvalidArg "Insights returned an invalid artifact URL."
    uri

let private transferClient =
    new HttpClient(new HttpClientHandler(AllowAutoRedirect = false, UseCookies = false), Timeout = TimeSpan.FromMinutes(10.0))

let private put location filename =
    use input = File.OpenRead(filename)
    use message = new HttpRequestMessage(HttpMethod.Put, getUri location)
    use content = new StreamContent(input)
    message.Content <- content
    location.Headers |> Option.iter (Map.iter (fun name value -> message.Headers.TryAddWithoutValidation(name, value) |> ignore))
    try
        use response = transferClient.Send(message)
        if not response.IsSuccessStatusCode && response.StatusCode <> HttpStatusCode.PreconditionFailed then
            raiseExternalError $"Encrypted artifact upload failed ({int response.StatusCode})."
    with
    | :? HttpRequestException -> raiseExternalError "Could not upload an encrypted artifact."
    | :? OperationCanceledException -> raiseExternalError "Encrypted artifact upload timed out."

let private withPrivateDirectory action =
    let path = Directory.CreateTempSubdirectory("terrabuild-archive-").FullName
    try action path
    finally IO.deleteAny path

let private download location (file: FileRecord) useCiphertext =
    withPrivateDirectory (fun directory ->
        use message = new HttpRequestMessage(HttpMethod.Get, getUri location)
        use response =
            try transferClient.Send(message, HttpCompletionOption.ResponseHeadersRead)
            with
            | :? HttpRequestException -> raiseExternalError "Could not download an encrypted artifact."
            | :? OperationCanceledException -> raiseExternalError "Encrypted artifact download timed out."
        if not response.IsSuccessStatusCode then
            raiseExternalError $"Encrypted artifact download failed ({int response.StatusCode})."
        let ciphertext = Path.Combine(directory, "archive")
        do
            use source = response.Content.ReadAsStream()
            use destination = File.Open(ciphertext, FileMode.CreateNew)
            let buffer = Array.zeroCreate<byte> 8192
            let mutable count = source.Read(buffer)
            let mutable size = 0L
            while count > 0 do
                size <- size + int64 count
                if size > file.Size then raiseInvalidArg "Artifact exceeds its signed size."
                destination.Write(buffer, 0, count)
                count <- source.Read(buffer)
            if size <> file.Size then raiseInvalidArg "Artifact does not match its signed size."
        useCiphertext ciphertext)

type private Pending = { Root: WorkspaceVault.Root; Files: ConcurrentDictionary<string, string * FileRecord> }

type private Storage(origin: Uri, workspace, repository: Repository, token, keyId, identityKind, identityId, keys, initialState: WorkspaceVault.KeyState) =
    let route = WorkspaceVault.path workspace ("/repositories/" + repository.RepositoryId)
    let accepted = ConcurrentDictionary<string, Manifest * Accepted>()
    let pending = ConcurrentDictionary<string * string, Pending>()
    let mutable state = initialState
    let mutable buildId: (unit -> string) option = None

    let request method suffix body =
        WorkspaceVault.request origin method (route + suffix) (Some token) body

    let withRoot (rootId: string) version action =
        let getRoot (current: WorkspaceVault.KeyState) =
            current.Roots |> List.tryFind (fun root -> root.RootId = rootId && root.Version = version && root.Status <> "retired")
        let current =
            if getRoot state |> Option.isSome then state
            else
                let refreshed = WorkspaceVault.getKeyState origin workspace token
                state <- refreshed
                refreshed
        let root = getRoot current |> Option.defaultWith (fun () -> raiseInvalidArg "The artifact workspace key version is unavailable.")
        let secret = WorkspaceVault.unwrapRoot origin workspace identityKind identityId keyId keys current root
        try action secret
        finally CryptographicOperations.ZeroMemory(secret)

    let fetch entries =
        let response: Lookup = request HttpMethod.Post "/lookup" (Some (Json.Serialize {| Entries = entries |}))
        for row in response.Entries do
            if List.contains row.Entry entries && row.AcceptedAt > DateTimeOffset.MinValue &&
               VaultCrypto.hash row.Manifest = row.Digest && VaultCrypto.verify row.SigningKey row.Signature row.Manifest then
                let manifest = Json.Deserialize<Manifest> row.Manifest
                if manifest.Version = 2 && manifest.WorkspaceId = workspace && manifest.RepositoryId = repository.RepositoryId &&
                   manifest.Entry = row.Entry && manifest.Generation = row.Generation && manifest.PublisherKeyId = row.PublisherKeyId &&
                   manifest.Files.Length = (manifest.Files |> List.distinctBy _.Kind |> List.length) then
                    accepted[row.Entry] <- manifest, row

    let lookup entry =
        let current () =
            match accepted.TryGetValue(entry) with
            | true, (manifest, response) when response.Locations |> List.forall (fun location -> location.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(5.0)) ->
                Some (manifest, response)
            | _ -> None
        match current () with
        | Some value -> Some value
        | None ->
            fetch [ entry ]
            current ()

    let resolve path =
        let entry, filename = splitPath path
        lookup entry |> Option.bind (fun (manifest, response) ->
            let findKind =
                if filename = "manifest" then Some "manifest"
                else [ "logs"; "outputs" ] |> List.tryFind (fun kind -> filename = kind + "-" + manifest.Generation)
            findKind |> Option.bind (fun kind ->
                let file = manifest.Files |> List.tryFind (fun file -> file.Kind = kind)
                let location = response.Locations |> List.tryFind (fun item -> item.Kind = kind)
                match file, location with
                | Some file, Some location -> Some (manifest, file, location)
                | _ -> None))

    let abort entry generation =
        match pending.TryRemove((entry, generation)) with
        | true, transaction ->
            for filename, _ in transaction.Files.Values do IO.deleteAny filename
        | _ -> ()

    interface Contracts.IBatchStorage with
        member _.PrepareReads entries =
            try fetch entries
            with :? TerrabuildException -> ()

    interface Contracts.IStorage with
        member _.Name = "Encrypted Insights Artifact Storage"
        member _.Exists path = resolve path |> Option.isSome
        member _.TryDownload path =
            resolve path |> Option.map (fun (manifest, file, location) ->
                let output = IO.getTempFilename()
                try
                    download location file (fun ciphertext ->
                        withRoot manifest.RootId manifest.RootVersion (fun root ->
                            Encryption.decryptWorkspace root (context manifest file.Kind) (Convert.FromHexString(file.Sha256)) ciphertext output))
                    output
                with _ ->
                    IO.deleteAny output
                    reraise())
        member _.Upload path source =
            let entry, filename = splitPath path
            let separator = filename.IndexOf('-')
            if separator < 0 then raiseInvalidArg "Missing artifact generation."
            let kind, generation = filename[..separator - 1], filename[separator + 1..]
            let transaction = pending.GetOrAdd((entry, generation), fun _ ->
                let root = state.Roots |> List.tryFind (fun root -> root.Status = "active")
                           |> Option.defaultWith (fun () -> raiseInvalidArg "The Insights workspace is uninitialized.")
                { Root = root; Files = ConcurrentDictionary() })
            let manifest = {
                Version = 2; WorkspaceId = workspace; BuildId = ""; RepositoryId = repository.RepositoryId; Entry = entry
                Generation = generation; ProductionId = generation; RootId = transaction.Root.RootId
                RootVersion = transaction.Root.Version; PublisherKeyId = keyId; Successful = false; Files = []
            }
            let encrypted = withRoot manifest.RootId manifest.RootVersion (fun root ->
                Encryption.encryptWorkspace root (context manifest kind) source)
            let file = { Kind = kind; Sha256 = (Hash.sha256file encrypted).ToLowerInvariant(); Size = IO.size encrypted }
            if not (transaction.Files.TryAdd(kind, (encrypted, file))) then
                IO.deleteAny encrypted
                raiseInvalidArg "Duplicate payload for this publication generation."

    interface Contracts.IWorkspaceStorage with
        member _.BindBuild getId = buildId <- Some getId
        member _.Abort entry generation = abort entry generation
        member _.Commit entry generation productionId successful =
            let transaction = pending[(entry, generation)]
            try
                let manifest = {
                    Version = 2; WorkspaceId = workspace; BuildId = (Option.get buildId)(); RepositoryId = repository.RepositoryId; Entry = entry
                    Generation = generation; ProductionId = defaultArg productionId generation; RootId = transaction.Root.RootId
                    RootVersion = transaction.Root.Version; PublisherKeyId = keyId; Successful = successful
                    Files = transaction.Files.Values |> Seq.map snd |> Seq.sortBy _.Kind |> List.ofSeq
                }
                let serialized = Json.Serialize manifest
                let signed = VaultCrypto.sign keys serialized
                let body = Some (Json.Serialize {| Manifest = serialized; Signature = signed |})
                let prepared: Publication = request HttpMethod.Post "/publications/prepare" body
                let receipt =
                    if prepared.Accepted then prepared
                    else
                        for location in defaultArg prepared.Locations [] do
                            let filename, _ = transaction.Files[location.Kind]
                            put location filename
                        request HttpMethod.Post "/publications/finalize" body
                if not receipt.Accepted || receipt.Digest <> Some (VaultCrypto.hash serialized) then
                    raiseInvalidArg "Insights did not accept the artifact publication."
                accepted.TryRemove(entry) |> ignore
            finally abort entry generation

let connect (origin: Uri) workspace (repositoryName: string) ciAuth humanSession =
    let credentials =
        match ciAuth, humanSession with
        | Some (auth: Auth.SpaceAuth), _ ->
            let credential, keys = VaultCrypto.ciCredentials origin workspace auth.Token
            try
                let exchanged = WorkspaceVault.exchange origin workspace credential
                Some (exchanged.AccessToken, exchanged.KeyId, "ci", exchanged.KeyId, keys)
            with _ ->
                (keys :> IDisposable).Dispose()
                reraise()
        | None, Some (session: Insights.Credential) when session.ExpiresAt > DateTimeOffset.UtcNow ->
            let userId, keyId, keys = WorkspaceVault.humanKeys origin session.Token
            Some (session.Token, keyId, "user", userId, keys)
        | _ -> None

    credentials |> Option.map (fun (token, keyId, kind, principal, keys) ->
        try
            let repository = WorkspaceVault.request<Repository> origin HttpMethod.Get
                                (WorkspaceVault.path workspace ("/repository?name=" + Uri.EscapeDataString(repositoryName))) (Some token) None
            let originName = origin.GetLeftPart(UriPartial.Authority)
            let file = mapping originName workspace repositoryName
            Path.GetDirectoryName(file) |> nonNull |> IO.createDirectory
            IO.writeTextFileAtomic file repository.RepositoryId
            let state = WorkspaceVault.getKeyState origin workspace token
            let active = state.Roots |> List.tryFind (fun root -> root.Status = "active")
            let canWrite = state.CanWrite && (active |> Option.exists (fun root -> state.Grants |> List.exists (fun grant -> grant.RootId = root.RootId)))
            if state.Grants.IsEmpty then eprintfn "Insights workspace key is unavailable. Initialize the workspace or ask an administrator for access."
            let storage = Storage(origin, workspace, repository, token, keyId, kind, principal, keys, state) :> Contracts.IStorage
            storage, scope originName workspace repository.RepositoryId, canWrite, token, repository.RepositoryId
        with _ ->
            (keys :> IDisposable).Dispose()
            reraise())
