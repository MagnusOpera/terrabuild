namespace Storages
open System.IO
open System.Net
open System.Net.Http
open Serilog


module private CloudflareHttp =
    let Client = new HttpClient()


type CloudflareR2(?httpClient: HttpClient) =
    let http = defaultArg httpClient CloudflareHttp.Client

    interface IRemoteStorageBackend with
        override _.Exists id location =
            try
                use request = new HttpRequestMessage(HttpMethod.Head, location)
                use response = http.SendAsync(request).Result

                if response.StatusCode = HttpStatusCode.NotFound then
                    false
                else
                    response.EnsureSuccessStatusCode() |> ignore
                    true
            with
            | exn ->
                Log.Fatal(exn, "CloudflareR2: failed to check '{Id}'", id)
                reraise()

        override _.TryDownload id location =
            let tmpFile = Path.GetTempFileName()

            try
                use response = http.GetAsync(location, HttpCompletionOption.ResponseHeadersRead).Result

                if response.StatusCode = HttpStatusCode.NotFound then
                    File.Delete(tmpFile)
                    None
                else
                    response.EnsureSuccessStatusCode() |> ignore
                    use source = response.Content.ReadAsStream()
                    use destination = File.Create(tmpFile)
                    source.CopyTo(destination)
                    Log.Debug("CloudflareR2: download of '{Id}' successful", id)
                    Some tmpFile
            with
            | exn ->
                File.Delete(tmpFile)
                Log.Fatal(exn, "CloudflareR2: failed to download '{Id}'", id)
                reraise()

        override _.Upload id location summaryFile =
            try
                use source = File.OpenRead(summaryFile)
                use content = new StreamContent(source)
                use response = http.PutAsync(location, content).Result
                response.EnsureSuccessStatusCode() |> ignore
                Log.Debug("CloudflareR2: upload of '{Id}' successful", id)
            with
            | exn ->
                Log.Fatal(exn, "CloudflareR2: upload of '{Id}' failed", id)
                reraise()
