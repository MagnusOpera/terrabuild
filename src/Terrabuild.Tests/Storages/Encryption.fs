module Terrabuild.Tests.Storages.Encryption
open FsUnit
open NUnit.Framework
open Encryption

[<Test>]
let ``verify encrypt/decrypt``() =
    let plainFile = IO.getTempFilename()
    let encryptedFile = IO.getTempFilename()
    let decryptedFile = IO.getTempFilename()
    try
        let masterKeyString = "tagada"
        let id = "12345/build/6789"

        let plainContent = "this is a secret content"

        plainContent |> IO.writeTextFile plainFile
        plainFile |> isEncryptedArtifact |> should equal false

        let masterKey = masterKeyFromString masterKeyString
        let encKey, macKey = deriveKeys masterKey id
        encryptFileStreaming encKey macKey plainFile encryptedFile
        System.IO.FileInfo(encryptedFile).Length |> should not' (equal plainContent.Length)
        encryptedFile |> isEncryptedArtifact |> should equal true

        decryptFileStreaming encKey macKey encryptedFile decryptedFile
        decryptedFile |> isEncryptedArtifact |> should equal false

        let decryptedContent = IO.readTextFile decryptedFile
        decryptedContent |> should equal plainContent
    finally
        IO.deleteAny decryptedFile
        IO.deleteAny encryptedFile
        IO.deleteAny plainFile

