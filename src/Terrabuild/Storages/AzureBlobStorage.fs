namespace Storages
open Azure.Storage.Blobs
open Serilog
open Encryption


type AzureArtifactLocationOutput = {
    Uri: string
}

type AzureBlobStorage(api: Contracts.IApiClient, masterKeyString: string option) =
    let masterKey = masterKeyString |> Option.map masterKeyFromString

    let getBlobClient path =
        let uri = api.GetArtifact path
        let container = BlobContainerClient(uri)
        let blobClient = container.GetBlobClient(path)
        blobClient


    interface Contracts.IStorage with
        override _.Name = "Azure Blob Storage"

        override _.Exists id =
            let blobClient = getBlobClient id
            try
                let res = blobClient.Exists()
                res.Value
            with
            | :? Azure.RequestFailedException as exn when exn.Status = 404 -> false
            | exn ->
                Log.Fatal(exn, "AzureBlobStorage: failed to download '{Id}'", id)
                reraise()


        override _.TryDownload id =
            let blobClient = getBlobClient id
            let tmpFile = IO.getTempFilename()
            try
                blobClient.DownloadTo(tmpFile) |> ignore

                match masterKey with
                | Some masterKey ->
                    // check if file is encrypted
                    if isEncryptedArtifact tmpFile then
                        let decryptedFile = IO.getTempFilename()
                        let encKey, macKey = deriveKeys masterKey id
                        decryptFileStreaming encKey macKey tmpFile decryptedFile
                        IO.deleteAny tmpFile
                        IO.moveFile decryptedFile tmpFile
                | _ -> ()

                Log.Debug("AzureBlobStorage: download of '{Id}' successful", id)
                Some tmpFile
            with
            | :? Azure.RequestFailedException as exn when exn.Status = 404 ->
                Log.Fatal("AzureBlobStorage: '{Id}' does not exist", id)
                System.IO.File.Delete(tmpFile)
                None
            | exn ->
                Log.Fatal(exn, "AzureBlobStorage: failed to download '{Id}'", id)
                reraise()


        override _.Upload id summaryFile =
            try
                match masterKey with
                | Some masterKey ->
                    let encryptedFile = IO.getTempFilename()
                    let encKey, macKey = deriveKeys masterKey id
                    encryptFileStreaming encKey macKey summaryFile encryptedFile
                    IO.deleteAny summaryFile
                    IO.moveFile encryptedFile summaryFile
                | _ -> ()

                let blobClient = getBlobClient id
                blobClient.Upload(summaryFile, true) |> ignore
                Log.Debug("AzureBlobStorage: upload of '{Id}' successful", id)
            with
            | exn ->
                Log.Fatal(exn, "AzureBlobStorage: upload of '{Id}' failed", id)
                reraise()
