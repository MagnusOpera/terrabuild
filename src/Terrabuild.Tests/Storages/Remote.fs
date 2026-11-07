module Terrabuild.Tests.Storages.Remote
open System
open System.IO
open System.Net
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open Contracts
open Errors
open FsUnit
open FsUnitTyped
open NUnit.Framework
open Storages


type private ArtifactLocationOutput =
    { Uri: string
      Provider: string option }


let private artifactPath = "project/build/hash/manifest"

let private readLocations provider path expiresAt : ArtifactLocation list =
    [ for operation in [ "get"; "head" ] ->
        { ArtifactLocation.Path = path
          Operation = operation
          Uri = $"https://storage.example/{path}"
          Provider = provider
          ExpiresAt = expiresAt } ]

let private found entry locations : ArtifactLookup =
    { ArtifactLookup.Entry = entry
      Found = true
      Locations = locations }

let private missing entry : ArtifactLookup =
    { ArtifactLookup.Entry = entry
      Found = false
      Locations = [] }

type private FakeApiClient(provider: string option, ?lookup: string list -> ArtifactLookup list) =
    let uploads = ResizeArray<string>()
    let lookupCalls = ResizeArray<string list>()
    let lookup = defaultArg lookup (List.map (fun entry ->
        found entry (readLocations provider (entry + "/manifest") (DateTime.UtcNow.AddMinutes(2.0)))))
    member _.Uploads = uploads |> Seq.toList
    member _.LookupCalls = lookupCalls |> Seq.toList

    interface IApiClient with
        member _.StartBuild() = ()
        member _.UploadBuildGraph _graphHash _environment _nodes = ()
        member _.CompleteBuild _success = ()
        member _.AddArtifact _project _projectName _target _projectHash _targetHash _files _success _startedAt _endedAt _provenance = ()
        member _.UseArtifact _projectHash _hash _productionId = ()

        member _.LookupArtifacts entries =
            lookupCalls.Add(entries)
            lookup entries

        member _.GetUploadLocation path =
            uploads.Add(path)
            Uri($"https://storage.example/{path}"), provider

        member _.GetCommitGraph repository commit _environment =
            { CommitGraph.Repository = repository
              Commit = commit
              GraphHash = "graph"
              Nodes = [] }


type private RecordingBackend() =
    let calls = ResizeArray<string * string * Uri * string option>()

    member _.Calls = calls |> Seq.toList

    interface IRemoteStorageBackend with
        member _.Exists id location =
            calls.Add("head", id, location, None)
            true

        member _.TryDownload id location =
            calls.Add("get", id, location, None)
            Some "download"

        member _.Upload id location summaryFile =
            calls.Add("put", id, location, Some summaryFile)


type private RecordingHandler(statusCode: HttpStatusCode, responseBody: string) =
    inherit HttpMessageHandler()

    let calls = ResizeArray<HttpMethod * Uri * string option>()

    member _.Calls = calls |> Seq.toList

    override _.SendAsync(request: HttpRequestMessage, cancellationToken: CancellationToken) =
        let body =
            match request.Content with
            | null -> None
            | content -> content.ReadAsStringAsync(cancellationToken).Result |> Some

        let requestUri =
            request.RequestUri
            |> Option.ofObj
            |> Option.defaultWith (fun () -> invalidOp "Expected an absolute request URI")

        calls.Add(request.Method, requestUri, body)
        let response = new HttpResponseMessage(statusCode)
        response.Content <- new StringContent(responseBody)
        Task.FromResult(response)


[<TestCase("{\"uri\":\"https://example.invalid\"}")>]
[<TestCase("{\"uri\":\"https://example.invalid\",\"provider\":null}")>]
let ``missing or null provider deserializes as Azure`` json =
    let output = Json.Deserialize<ArtifactLocationOutput>(json)
    Api.Artifact.normalizeProvider output.Provider |> shouldEqual None


[<Test>]
let ``remote storage requests operation-specific locations and selects Azure`` () =
    let api = FakeApiClient(None)
    let azure = RecordingBackend()
    let cloudflare = RecordingBackend()
    let storage =
        Storages.Factory.RemoteStorage(
            api,
            azureBackend = azure,
            cloudflareBackend = cloudflare
        ) :> IStorage

    storage.Exists artifactPath |> should equal true
    storage.TryDownload artifactPath |> shouldEqual (Some "download")
    storage.Upload artifactPath "summary.zip"

    api.LookupCalls |> should equal [ [ "project/build/hash" ] ]
    api.Uploads |> should equal [ artifactPath ]

    azure.Calls |> List.map (fun (operation, _, _, _) -> operation)
    |> should equal [ "head"; "get"; "put" ]
    cloudflare.Calls |> should be Empty


[<Test>]
let ``remote storage selects Cloudflare R2`` () =
    let api = FakeApiClient(Some "r2")
    let azure = RecordingBackend()
    let cloudflare = RecordingBackend()
    let storage =
        Storages.Factory.RemoteStorage(
            api,
            azureBackend = azure,
            cloudflareBackend = cloudflare
        ) :> IStorage

    storage.Exists artifactPath |> should equal true

    azure.Calls |> should be Empty
    cloudflare.Calls |> List.map (fun (operation, _, _, _) -> operation)
    |> should equal [ "head" ]


[<Test>]
let ``remote storage rejects unknown providers`` () =
    let api = FakeApiClient(Some "unknown")
    let storage = Storages.Factory.RemoteStorage(api) :> IStorage

    let ex =
        Assert.Throws<TerrabuildException>(
            Action(fun () -> storage.Exists artifactPath |> ignore)
        )
        |> Option.ofObj
        |> Option.defaultWith (fun () -> failwith "Expected TerrabuildException")

    ex.Area |> should equal ErrorArea.External
    ex.Message |> should equal "Unsupported artifact storage provider 'unknown'."


[<Test>]
let ``Azure backend appends the artifact path to the container SAS URI`` () =
    let storage = AzureBlobStorage()
    let container = Uri("https://account.blob.core.windows.net/cache?sig=secret")

    storage.GetBlobUri "project/target/artifact.zip" container
    |> should equal (Uri("https://account.blob.core.windows.net/cache/project/target/artifact.zip?sig=secret"))


[<Test>]
let ``Cloudflare backend uses signed URLs directly for every operation`` () =
    use handler = new RecordingHandler(HttpStatusCode.OK, "downloaded")
    use client = new HttpClient(handler)
    let storage = new CloudflareR2(client) :> IRemoteStorageBackend
    let location = Uri("https://r2.example/object?signature=value")
    let uploadFile = Path.GetTempFileName()
    File.WriteAllText(uploadFile, "uploaded")

    try
        storage.Exists "artifact" location |> should equal true
        let downloadFile = storage.TryDownload "artifact" location |> Option.get

        try
            File.ReadAllText(downloadFile) |> should equal "downloaded"
        finally
            File.Delete(downloadFile)

        storage.Upload "artifact" location uploadFile

        handler.Calls
        |> should equal
            [ HttpMethod.Head, location, None
              HttpMethod.Get, location, None
              HttpMethod.Put, location, Some "uploaded" ]
    finally
        File.Delete(uploadFile)


[<Test>]
let ``Cloudflare backend maps not found responses`` () =
    use handler = new RecordingHandler(HttpStatusCode.NotFound, "")
    use client = new HttpClient(handler)
    let storage = new CloudflareR2(client) :> IRemoteStorageBackend
    let location = Uri("https://r2.example/missing")

    storage.Exists "artifact" location |> should equal false
    storage.TryDownload "artifact" location |> shouldEqual None

[<Test>]
let ``authorization failures propagate from artifact lookup`` () =
    let api = FakeApiClient(Some "r2", lookup = fun _ -> raise (TerrabuildException("Forbidden", ErrorArea.Auth)))
    let storage = Storages.Factory.RemoteStorage(api) :> IStorage
    Assert.Throws<TerrabuildException>(Action(fun () -> storage.Exists artifactPath |> ignore)) |> ignore
    Assert.Throws<TerrabuildException>(Action(fun () -> storage.TryDownload artifactPath |> ignore)) |> ignore
    api.Uploads |> should be Empty

[<Test>]
let ``prefetch shares operation-specific locations with storage reads`` () =
    let entry = "project/build/hash"
    let api = FakeApiClient(Some "r2")
    let backend = RecordingBackend()
    let storage = Storages.Factory.RemoteStorage(api, cloudflareBackend = backend)
    (storage :> IBatchStorage).PrepareReads [ entry; entry ]
    (storage :> IStorage).TryDownload artifactPath |> shouldEqual (Some "download")
    (storage :> IStorage).Exists artifactPath |> should equal true
    api.LookupCalls |> should equal [ [ entry ] ]
    api.Uploads |> should be Empty
    backend.Calls.Length |> should equal 2

[<Test>]
let ``upload invalidates a cached lookup miss`` () =
    let entry = "project/build/hash"
    let mutable published = false
    let api = FakeApiClient(Some "r2", lookup = fun _ ->
        if published then [ found entry (readLocations (Some "r2") artifactPath (DateTime.UtcNow.AddMinutes(2.0))) ]
        else [ missing entry ])
    let storage = Storages.Factory.RemoteStorage(api, cloudflareBackend = RecordingBackend())
    let remote = storage :> IStorage
    (storage :> IBatchStorage).PrepareReads [ entry ]
    remote.TryDownload artifactPath |> shouldEqual None
    remote.Exists artifactPath |> should equal false
    api.LookupCalls |> should equal [ [ entry ] ]
    remote.Upload artifactPath "source"
    published <- true
    remote.TryDownload artifactPath |> shouldEqual (Some "download")
    api.LookupCalls |> should equal [ [ entry ]; [ entry ] ]
    api.Uploads |> should equal [ artifactPath ]

[<TestCase(true)>]
[<TestCase(false)>]
let ``reads refresh expired locations or locations for a new generation`` expired =
    let entry = "project/build/hash"
    let path = entry + "/logs-new"
    let mutable calls = 0
    let api = FakeApiClient(Some "r2", lookup = fun _ ->
        calls <- calls + 1
        let file, expiry =
            if calls > 1 then path, DateTime.UtcNow.AddMinutes(2.0)
            elif expired then path, DateTime.UtcNow.AddSeconds(-1.0)
            else entry + "/logs-old", DateTime.UtcNow.AddMinutes(2.0)
        [ found entry (readLocations (Some "r2") file expiry) ])
    let storage = Storages.Factory.RemoteStorage(api, cloudflareBackend = RecordingBackend())
    (storage :> IBatchStorage).PrepareReads [ entry ]
    (storage :> IStorage).TryDownload path |> shouldEqual (Some "download")
    api.LookupCalls |> should equal [ [ entry ]; [ entry ] ]
    api.Uploads |> should be Empty

[<Test>]
let ``a failed prefetch retries the batch endpoint on demand`` () =
    let entry = "project/build/hash"
    let mutable calls = 0
    let api = FakeApiClient(None, lookup = fun _ ->
        calls <- calls + 1
        if calls = 1 then failwith "Temporary API failure"
        [ found entry (readLocations None artifactPath (DateTime.UtcNow.AddMinutes(2.0))) ])
    let storage = Storages.Factory.RemoteStorage(api, azureBackend = RecordingBackend())
    (storage :> IBatchStorage).PrepareReads [ entry ]
    (storage :> IStorage).TryDownload artifactPath |> shouldEqual (Some "download")
    api.LookupCalls |> should equal [ [ entry ]; [ entry ] ]
    api.Uploads |> should be Empty

[<Test>]
let ``batch lookup requests are bounded`` () =
    let api = FakeApiClient(None, lookup = List.map missing)
    let storage = Storages.Factory.RemoteStorage(api)
    (storage :> IBatchStorage).PrepareReads [ for index in 1..130 -> $"project/build/{index}" ]
    api.LookupCalls |> List.map List.length |> should equal [ 64; 64; 2 ]
