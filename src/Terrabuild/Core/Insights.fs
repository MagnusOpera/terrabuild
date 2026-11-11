module Insights

open System
open System.Diagnostics
open System.IO
open System.Net
open System.Net.Http
open System.Text
open System.Text.Json
open Errors

type internal Credential = { Token: string; ExpiresAt: DateTimeOffset }

let private sessionAccount (origin: Uri) =
    "insights-session/" + CredentialStore.identifier origin.AbsoluteUri

let private keyAccount (origin: Uri) workspaceId =
    "insights-key/" + CredentialStore.identifier (origin.AbsoluteUri + workspaceId)

let private readCredential (store: CredentialStore.Store) origin =
    store.Read (sessionAccount origin) |> Option.map CredentialStore.deserialize<Credential>

let private preferencePath (origin: Uri) =
    Path.Combine(Cache.createTerrabuildProfile(), "insights-preferences", CredentialStore.identifier origin.AbsoluteUri)

let private lockSession origin =
    let preference = preferencePath origin
    Directory.CreateDirectory(Path.GetDirectoryName(preference) |> nonNull) |> ignore
    ExclusiveFileLock.acquire "Insights session" (preference + ".lock")

let internal session origin =
    if CredentialStore.isCi () then None
    else
        match readCredential (CredentialStore.desktop ()) origin with
        | None when File.Exists(preferencePath origin) ->
            raiseInvalidArg "Insights sign-in is unavailable. Run terrabuild insights login again."
        | credential -> credential

let internal readKey origin workspaceId =
    let store = CredentialStore.desktop ()
    store.Read (keyAccount origin workspaceId) |> Option.map Auth.decodeKey

let importKey workspaceId =
    let origin = Api.Endpoint.origin ()
    let secret =
        if Console.IsInputRedirected then
            Console.ReadLine() |> Option.ofObj |> Option.defaultValue ""
        else
            eprintf "Workspace master key: "
            let buffer = StringBuilder()
            let mutable complete = false
            while not complete do
                let key = Console.ReadKey(intercept = true)
                match key.Key with
                | ConsoleKey.Enter -> complete <- true
                | ConsoleKey.Backspace when buffer.Length > 0 -> buffer.Length <- buffer.Length - 1
                | _ when not (Char.IsControl(key.KeyChar)) -> buffer.Append(key.KeyChar) |> ignore
                | _ -> ()
            eprintfn ""
            buffer.ToString()
    if String.IsNullOrWhiteSpace(secret) then raiseInvalidArg "The master key must not be empty."
    let store = CredentialStore.desktop ()
    store.Write (keyAccount origin workspaceId) (Auth.deriveKey workspaceId secret)
    eprintfn "Workspace key stored."

let forgetKey workspaceId =
    let origin = Api.Endpoint.origin ()
    let store = CredentialStore.desktop ()
    store.Delete (keyAccount origin workspaceId)

let private openBrowser (url: string) =
    match Uri.TryCreate(url, UriKind.Absolute) with
    | true, NonNull uri when uri.Scheme = "https" || (uri.Scheme = "http" && uri.IsLoopback) ->
        try Process.Start(ProcessStartInfo(uri.AbsoluteUri, UseShellExecute = true)) |> ignore
        with _ -> ()
    | _ -> raiseExternalError "Insights returned an invalid browser URL."

let private client () =
    let handler = new HttpClientHandler(AllowAutoRedirect = false, UseCookies = false)
    new HttpClient(handler, Timeout = TimeSpan.FromSeconds(90.0))

let private request (http: HttpClient) (origin: Uri) (method: HttpMethod) (path: string) (token: string option) (body: string option) =
    use message = new HttpRequestMessage(method, Uri(origin, path))
    token |> Option.iter (fun value -> message.Headers.Authorization <- Headers.AuthenticationHeaderValue("Bearer", value))
    body |> Option.iter (fun value -> message.Content <- new StringContent(value, Encoding.UTF8, "application/json"))
    use response =
        try http.Send(message)
        with
        | :? HttpRequestException -> raiseExternalError $"Could not reach Insights at {origin.Authority}."
        | :? OperationCanceledException -> raiseExternalError "The Insights request timed out. Try again."
    let content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    let json =
        match response.Content.Headers.ContentType with
        | NonNull contentType when contentType.MediaType = "application/json" && not (String.IsNullOrWhiteSpace(content)) -> content
        | _ -> "{}"
    use document = JsonDocument.Parse(json)
    let data = document.RootElement.Clone()
    response.StatusCode, response.IsSuccessStatusCode, data

let private tryString key (data: JsonElement) =
    match data.TryGetProperty(key: string) with
    | true, value when value.ValueKind = JsonValueKind.String -> value.GetString() |> Option.ofObj
    | _ -> None

let private requiredString key data =
    tryString key data |> Option.defaultWith (fun () -> raiseExternalError "Insights returned an invalid response.")

let private requiredInt key (data: JsonElement) =
    match data.TryGetProperty(key: string) with
    | true, value when value.ValueKind = JsonValueKind.Number -> value.GetInt32()
    | _ -> raiseExternalError "Insights returned an invalid response."

let private fail status data =
    let message =
        tryString "title" data
        |> Option.defaultValue $"Insights request failed ({int status})."
    raiseExternalError message

let login noOpen =
    let origin = Api.Endpoint.origin ()
    use http = client ()
    let body = Json.Serialize {| client_id = "terrabuild" |} |> Some
    let status, ok, code = request http origin HttpMethod.Post "/api/auth/device/code" None body
    if not ok then fail status code
    let url = requiredString "verification_uri" code
    let userCode = requiredString "user_code" code
    eprintfn $"Open {url} and enter code {userCode}."
    if not noOpen then openBrowser url
    let body =
        Json.Serialize {| client_id = "terrabuild"; device_code = requiredString "device_code" code
                          grant_type = "urn:ietf:params:oauth:grant-type:device_code" |} |> Some
    let deadline = DateTimeOffset.UtcNow.AddSeconds(float (requiredInt "expires_in" code))
    let rec poll interval =
        if DateTimeOffset.UtcNow.AddSeconds(float interval) >= deadline then
            raiseExternalError "The Insights sign-in code expired. Run terrabuild insights login again."
        Threading.Thread.Sleep(TimeSpan.FromSeconds(float interval))
        let status, ok, data = request http origin HttpMethod.Post "/api/auth/device/token" None body
        if ok then
            { Token = requiredString "access_token" data
              ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(float (requiredInt "expires_in" data)) }
        else
            match tryString "error" data with
            | Some "authorization_pending" -> poll interval
            | Some "slow_down" -> poll (interval + 5)
            | Some "access_denied" -> raiseExternalError "Insights sign-in was denied."
            | _ when status = HttpStatusCode.TooManyRequests -> poll (interval + 5)
            | _ -> fail status data
    let credential = poll (max 1 (requiredInt "interval" code))
    let store = CredentialStore.desktop ()
    try
        use lease = lockSession origin
        // Keep Insights selected if the keyring later loses or hides the session.
        File.WriteAllBytes(preferencePath origin, Array.empty)
        store.Write (sessionAccount origin) (Json.Serialize credential)
    with _ ->
        try request http origin HttpMethod.Delete "/api/cli/session" (Some credential.Token) None |> ignore
        with _ -> ()
        reraise()
    eprintfn "Signed in to Insights."

let logout () =
    let origin = Api.Endpoint.origin ()
    use lease = lockSession origin
    let store = CredentialStore.desktop ()
    match readCredential store origin with
    | None -> ()
    | Some credential ->
        use http = client ()
        let status, ok, data = request http origin HttpMethod.Delete "/api/cli/session" (Some credential.Token) None
        if not ok then fail status data
        store.Delete (sessionAccount origin)
    File.Delete(preferencePath origin)


let internal artifactLookup (origin: Uri) (workspaceId: string) (credential: Credential) =
    let workspace = Uri.EscapeDataString(workspaceId)
    let path = $"/terrabuild/cli/workspaces/{workspace}/artifacts/lookup"
    let warned = ref 0
    let missing entries =
        entries |> List.map (fun entry -> ({ Entry = entry; Found = false; Locations = [] }: Contracts.ArtifactLookup))
    fun entries ->
        if credential.ExpiresAt <= DateTimeOffset.UtcNow then
            if Threading.Interlocked.Exchange(warned, 1) = 0 then
                eprintfn "Insights sign-in expired. Run terrabuild insights login again."
            missing entries
        else
            use http = client ()
            let body = Json.Serialize {| Entries = entries |} |> Some
            let status, ok, data = request http origin HttpMethod.Post path (Some credential.Token) body
            if ok then (data.GetRawText() |> Json.Deserialize<Api.Artifact.LookupOutput>).Entries
            else
                if Threading.Interlocked.Exchange(warned, 1) = 0 then
                    match status with
                    | HttpStatusCode.Unauthorized -> eprintfn "Insights sign-in expired or was revoked. Run terrabuild insights login again."
                    | HttpStatusCode.Forbidden -> eprintfn "Insights denied artifact access to this workspace."
                    | _ -> eprintfn $"Insights artifact lookup failed ({int status}). Using local builds."
                missing entries
