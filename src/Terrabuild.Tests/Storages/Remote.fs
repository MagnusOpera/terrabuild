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
open NUnit.Framework
open Storages


type private ArtifactLocationOutput =
    { Uri: string
      Provider: string option }


type private FakeApiClient(provider: string option) =
    let calls = ResizeArray<string * string>()

    member _.Calls = calls |> Seq.toList

    interface IApiClient with
        member _.StartBuild() = ()
        member _.UploadBuildGraph _graphHash _environment _nodes = ()
        member _.CompleteBuild _success = ()
        member _.AddArtifact _project _projectName _target _projectHash _targetHash _files _success _startedAt _endedAt = ()
        member _.UseArtifact _projectHash _hash = ()

        member _.GetArtifact path operation =
            calls.Add(path, operation)
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
    Api.Artifact.normalizeProvider output.Provider |> should equal None


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

    storage.Exists "artifact" |> should equal true
    storage.TryDownload "artifact" |> should equal (Some "download")
    storage.Upload "artifact" "summary.zip"

    api.Calls
    |> should equal
        [ "artifact", "head"
          "artifact", "get"
          "artifact", "put" ]

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

    storage.Exists "artifact" |> should equal true

    azure.Calls |> should be Empty
    cloudflare.Calls |> List.map (fun (operation, _, _, _) -> operation)
    |> should equal [ "head" ]


[<Test>]
let ``remote storage rejects unknown providers`` () =
    let api = FakeApiClient(Some "unknown")
    let storage = Storages.Factory.RemoteStorage(api) :> IStorage

    let ex =
        Assert.Throws<TerrabuildException>(
            Action(fun () -> storage.Exists "artifact" |> ignore)
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
    storage.TryDownload "artifact" location |> should equal None
