module Insights

open System
open System.Diagnostics
open System.IO
open System.Net
open System.Net.Http
open System.Text
open System.Text.Json
open Errors

let private origin () =
    let configured =
        Environment.GetEnvironmentVariable("TERRABUILD_API_URL")
        |> Option.ofObj
        |> Api.Endpoint.resolve
    if configured.Scheme <> "https" && not (configured.Scheme = "http" && configured.IsLoopback) then
        raiseInvalidArg "Insights user sign-in requires HTTPS."
    Uri(configured.GetLeftPart(UriPartial.Authority))

let private credentialPath (origin: Uri) =
    let key = Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(origin.AbsoluteUri)) |> Convert.ToHexString
    Path.Combine(Cache.createTerrabuildProfile(), "insights-" + key + ".json")

type private Credential = { Token: string; ExpiresAt: DateTimeOffset }

let private writeCredential path credential =
    let temporary = path + "." + Guid.NewGuid().ToString("N")
    let options = FileStreamOptions(Mode = FileMode.CreateNew, Access = FileAccess.Write)
    if not (OperatingSystem.IsWindows()) then
        options.UnixCreateMode <- Nullable (UnixFileMode.UserRead ||| UnixFileMode.UserWrite)
    try
        do
            use stream = new FileStream(temporary, options)
            use writer = new StreamWriter(stream)
            writer.Write(Json.Serialize credential)
        File.Move(temporary, path, true)
    finally
        File.Delete(temporary)

let private readCredential path =
    if File.Exists(path) then Some (File.ReadAllText(path) |> Json.Deserialize<Credential>)
    else None

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
    let origin = origin ()
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
    let path = credentialPath origin
    writeCredential path credential
    eprintfn "Signed in to Insights."

let logout () =
    let origin = origin ()
    let path = credentialPath origin
    match readCredential path with
    | None -> ()
    | Some credential ->
        use http = client ()
        let status, ok, data = request http origin HttpMethod.Delete "/api/cli/session" (Some credential.Token) None
        if not ok then fail status data
        File.Delete(path)
