namespace Storages
open Azure.Storage.Blobs
open Serilog


type AzureBlobStorage() =
    let getBlobClient path location =
        BlobContainerClient(location).GetBlobClient(path)

    member internal _.GetBlobUri path location =
        (getBlobClient path location).Uri

    interface IRemoteStorageBackend with
        override _.Exists id location =
            let blobClient = getBlobClient id location
            try
                let res = blobClient.Exists()
                res.Value
            with
            | :? Azure.RequestFailedException as exn when exn.Status = 404 -> false
            | exn ->
                Log.Fatal(exn, "AzureBlobStorage: failed to download '{Id}'", id)
                reraise()


        override _.TryDownload id location =
            let blobClient = getBlobClient id location
            let tmpFile = System.IO.Path.GetTempFileName()
            try
                blobClient.DownloadTo(tmpFile) |> ignore
                Log.Debug("AzureBlobStorage: download of '{Id}' successful", id)
                Some tmpFile
            with
            | :? Azure.RequestFailedException as exn when exn.Status = 404 ->
                Log.Fatal("AzureBlobStorage: '{Id}' does not exist", id)
                System.IO.File.Delete(tmpFile)
                None
            | exn ->
                System.IO.File.Delete(tmpFile)
                Log.Fatal(exn, "AzureBlobStorage: failed to download '{Id}'", id)
                reraise()


        override _.Upload id location summaryFile =
            try
                let blobClient = getBlobClient id location
                blobClient.Upload(summaryFile, true) |> ignore
                Log.Debug("AzureBlobStorage: upload of '{Id}' successful", id)
            with
            | exn ->
                Log.Fatal(exn, "AzureBlobStorage: upload of '{Id}' failed", id)
                reraise()
