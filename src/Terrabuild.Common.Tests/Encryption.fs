module Terrabuild.Common.Tests.Helpers.Encryption
open System.IO
open FsUnit
open NUnit.Framework
open Encryption

[<Test>]
let ``encrypt/decrypt with same master key shall success``() =
    let plainArchive = IO.getTempFilename()
    let salt = "7211d6d5254fb4e742c650b0"
    let masterKey = "tagada" |> masterKeyFromString salt
    let id = "12345/build/6789"

    let plainContent = "this is a secret content"

    plainContent |> IO.writeTextFile plainArchive
    plainArchive |> isEncrypted |> should equal false

    let encryptedArchive = encrypt (Some masterKey) id plainArchive
    System.IO.FileInfo(encryptedArchive).Length |> should not' (equal plainContent.Length)
    encryptedArchive |> isEncrypted |> should equal true

    let decryptedArchive = tryDecrypt (Some masterKey) id encryptedArchive
    decryptedArchive |> Option.isSome |> should equal true

    let decryptedArchive = decryptedArchive.Value
    decryptedArchive |> isEncrypted |> should equal false
    let decryptedContent = IO.readTextFile decryptedArchive
    decryptedContent |> should equal plainContent

    IO.deleteAny plainArchive
    IO.deleteAny encryptedArchive
    IO.deleteAny decryptedArchive


[<Test>]
let ``encrypt/decrypt with different master key shall fail``() =
    let plainArchive = IO.getTempFilename()
    let salt = "7211d6d5254fb4e742c650b0"
    let masterKey = "tagada" |> masterKeyFromString salt
    let masterKey2 = "pouet pouet" |> masterKeyFromString salt
    let id = "12345/build/6789"

    let plainContent = "this is a secret content"

    plainContent |> IO.writeTextFile plainArchive
    plainArchive |> isEncrypted |> should equal false

    let encryptedArchive = encrypt (Some masterKey) id plainArchive
    System.IO.FileInfo(encryptedArchive).Length |> should not' (equal plainContent.Length)
    encryptedArchive |> isEncrypted |> should equal true

    // different master key
    let decryptedArchive = tryDecrypt (Some masterKey2) id encryptedArchive
    decryptedArchive |> Option.isSome |> should equal false

    // no master key
    let decryptedArchive = tryDecrypt None id encryptedArchive
    decryptedArchive |> Option.isSome |> should equal false

    IO.deleteAny plainArchive
    IO.deleteAny encryptedArchive

[<Test>]
let ``decrypting unencrypted file with master key shall fail``() =
    let plainArchive = IO.getTempFilename()
    let salt = "7211d6d5254fb4e742c650b0"
    let masterKey = "tagada" |> masterKeyFromString salt
    let id = "12345/build/6789"

    let plainContent = "this is a secret content"

    plainContent |> IO.writeTextFile plainArchive
    plainArchive |> isEncrypted |> should equal false

    // different master key
    let decryptedArchive = tryDecrypt (Some masterKey) id plainArchive
    decryptedArchive |> Option.isSome |> should equal false

    IO.deleteAny plainArchive

[<Test>]
let ``decrypting unencrypted file with no master key shall succeed``() =
    let plainArchive = IO.getTempFilename()
    let id = "12345/build/6789"

    let plainContent = "this is a secret content"

    plainContent |> IO.writeTextFile plainArchive
    plainArchive |> isEncrypted |> should equal false

    let decryptedArchive = tryDecrypt None id plainArchive
    decryptedArchive |> Option.isSome |> should equal true
    decryptedArchive.Value |> should equal plainArchive

    IO.deleteAny plainArchive

[<Test>]
let ``workspace archive authenticates context and ciphertext before opening plaintext`` () =
    let input = IO.getTempFilename()
    let root = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)
    let directory = Directory.CreateTempSubdirectory()
    let output = Path.Combine(directory.FullName, "plain")
    let mutable archives = []
    try
        File.WriteAllText(input, "workspace artifact")
        let first = encryptWorkspace root "workspace/repository/entry/generation/logs" input
        archives <- first :: archives
        let second = encryptWorkspace root "workspace/repository/entry/generation/logs" input
        archives <- second :: archives
        let digest = File.ReadAllBytes(first) |> System.Security.Cryptography.SHA256.HashData
        Assert.That(File.ReadAllBytes(second)[8..23], Is.Not.EqualTo(File.ReadAllBytes(first)[8..23]))
        Assert.Throws<System.InvalidOperationException>(fun () ->
            decryptWorkspace root "workspace/other-repository/entry/generation/logs" digest first output) |> ignore
        Assert.That(File.Exists(output), Is.False)
        let wrongDigest = Array.zeroCreate<byte> 32
        Assert.Throws<System.InvalidOperationException>(fun () ->
            decryptWorkspace root "workspace/repository/entry/generation/logs" wrongDigest first output) |> ignore
        Assert.That(File.Exists(output), Is.False)
        decryptWorkspace root "workspace/repository/entry/generation/logs" digest first output
        Assert.That(File.ReadAllText(output), Is.EqualTo("workspace artifact"))
    finally
        archives |> List.iter IO.deleteAny
        IO.deleteAny input
        directory.Delete(true)
