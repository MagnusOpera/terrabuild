module Terrabuild.Tests.Storages.Encryption
open FsUnit
open NUnit.Framework
open Encryption

[<Test>]
let ``verify encrypt/decrypt``() =
    let masterKeyString = "tagada"
    let id = "12345/build/6789"

    let plainFile = IO.getTempFilename()
    let encryptedFile = IO.getTempFilename()
    let decryptedFile = IO.getTempFilename()

    printfn $"plainFile = {plainFile}"
    printfn $"encryptedFile = {encryptedFile}"
    printfn $"decryptedFile = {decryptedFile}"

    let plainContent = "this is a secret content"

    plainContent |> IO.writeTextFile plainFile

    let masterKey = masterKeyFromString masterKeyString
    let encKey, macKey = deriveKeys masterKey id
    encryptFileStreaming encKey macKey plainFile encryptedFile
    System.IO.FileInfo(encryptedFile).Length |> should not' (equal plainContent.Length)

    decryptFileStreaming encKey macKey encryptedFile decryptedFile

    let decryptedContent = IO.readTextFile decryptedFile
    decryptedContent |> should equal plainContent
