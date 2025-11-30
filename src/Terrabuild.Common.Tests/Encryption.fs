module Terrabuild.Common.Tests.Helpers.Encryption
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
