module Terrabuild.Tests.Core.Auth

open System
open System.IO
open FsUnitTyped
open NUnit.Framework

let private withJobStore action =
    let root = Path.Combine(Path.GetTempPath(), $"terrabuild-auth-tests-{Guid.NewGuid():N}")
    Directory.CreateDirectory(root) |> ignore
    try action (CredentialStore.job root) root
    finally Directory.Delete(root, true)

let private staging = Uri("https://staging.example/")
let private production = Uri("https://production.example/")
let private workspaceAuth token : Auth.SpaceAuth = { Token = token }

[<Test>]
let ``workspace credentials are origin scoped and logout works without an origin`` () =
    withJobStore (fun store root ->
        let stagingAuth = workspaceAuth "staging-token"
        let productionAuth = workspaceAuth "production-token"
        let other = workspaceAuth "other-token"
        Auth.save store root staging "workspace-a" stagingAuth
        Auth.save store root staging "workspace-b" other
        Auth.read store production "workspace-a" |> shouldEqual None

        Auth.save store root production "workspace-a" productionAuth
        let nextProcess = CredentialStore.job root
        Auth.read nextProcess staging "workspace-a" |> shouldEqual (Some stagingAuth)
        Auth.read nextProcess production "workspace-a" |> shouldEqual (Some productionAuth)
        if not (OperatingSystem.IsWindows()) then
            for file in Directory.EnumerateFiles(root) |> Seq.filter (fun path -> not (path.EndsWith(".lock"))) do
                File.GetUnixFileMode(file) |> shouldEqual (UnixFileMode.UserRead ||| UnixFileMode.UserWrite)

        Auth.remove nextProcess root "workspace-a"
        Auth.remove nextProcess root "workspace-a"
        Auth.read store staging "workspace-a" |> shouldEqual None
        Auth.read store production "workspace-a" |> shouldEqual None
        Auth.read store staging "workspace-b" |> shouldEqual (Some other))

[<Test>]
let ``CI derivation separates the authentication and key material by workspace`` () =
    let token = "tbci2_" + VaultCrypto.encode (Array.init 32 byte)
    let auth, first = VaultCrypto.ciCredentials staging "workspace-a" token
    let secondAuth, second = VaultCrypto.ciCredentials staging "workspace-a" token
    let otherAuth, other = VaultCrypto.ciCredentials staging "workspace-b" token
    use _first = first
    use _second = second
    use _other = other
    auth |> shouldEqual "8QBV39_tKDF4xU9WeB3IJONGKAd-WHOM9WqPsKrMMA8"
    secondAuth |> shouldEqual auth
    (otherAuth = auth) |> shouldEqual false
    let message = "signed artifact"
    let signature = VaultCrypto.sign first message
    VaultCrypto.encode first.SigningPublic |> shouldEqual "ZxR6kbYFJpbl1FHAIxY9vmB8DRY1SVDU8JWxwKUPb1c"
    signature |> shouldEqual "AVdE92VlvMkrZ_S7OF1QSbr9QLr784s6rJdHtZc5f_YK2tjkZYMSq8SFFrpKkhH8aEyxim2F-ylfIU53skkjAg"
    VaultCrypto.verify (VaultCrypto.encode second.SigningPublic) signature message |> shouldEqual true
    VaultCrypto.verify (VaultCrypto.encode other.SigningPublic) signature message |> shouldEqual false
    VaultCrypto.verify (VaultCrypto.encode second.SigningPublic) signature (message + "!") |> shouldEqual false
    VaultCrypto.verify (VaultCrypto.encode second.SigningPublic) "AA" message |> shouldEqual false
    let sealedValue = VaultCrypto.seal (VaultCrypto.encode first.WrappingPublic) (VaultCrypto.utf8 message)
    VaultCrypto.unseal second sealedValue |> shouldEqual (VaultCrypto.utf8 message)

[<Test>]
let ``native wrapping opens existing envelopes and preserves CI registration proofs`` () =
    let token = "tbci2_" + VaultCrypto.encode (Array.init 32 byte)
    let _, keys = VaultCrypto.ciCredentials staging "workspace-a" token
    use _keys = keys
    // Generated with the libsodium 1.0.20 binaries from the previous dependency.
    let peer = "VzCACrNA_LGM5REe2p1wX5E4i0HkVEy9EDullC2yIz4"
    let sealedBox = "PJ6hWv4xyMf7pzp3qUbcPjwcT_0SSO1rCVAspgEtjGSE8VL4rEaA9o4iILc8pQU6quH04_wC_83_cCj3JzXLQk_SW6yXAQ"
    VaultCrypto.encode keys.WrappingPublic |> shouldEqual "R_uFFZP78gLty1cVQxMP7jF8v2gzHjXxwVaFBbInPRw"
    VaultCrypto.wrappingProof keys peer "CI registration fixture" |> shouldEqual "--5NnLPO-VfWVFKeC40XK7THItljOMC8XT74v-ho0mE"
    VaultCrypto.unseal keys sealedBox |> shouldEqual (VaultCrypto.utf8 "workspace root fixture")

    let tampered = VaultCrypto.decode sealedBox
    tampered[tampered.Length - 1] <- tampered[tampered.Length - 1] ^^^ 1uy
    Assert.Throws<System.Security.Cryptography.CryptographicException>(fun () ->
        VaultCrypto.unseal keys (VaultCrypto.encode tampered) |> ignore) |> ignore
    Assert.Throws<System.Security.Cryptography.CryptographicException>(fun () ->
        VaultCrypto.unseal keys "AA" |> ignore) |> ignore
    Assert.Throws<System.Security.Cryptography.CryptographicException>(fun () ->
        VaultCrypto.wrappingProof keys (VaultCrypto.encode (Array.zeroCreate<byte> 32)) "CI registration fixture" |> ignore) |> ignore

[<Test>]
let ``account recovery authenticates the context and uses a fresh nonce`` () =
    let recovery = VaultCrypto.newRecovery ()
    let bundle = Array.init 64 byte
    let context = "https://staging.example\nuser-id\nkey-id"
    let first = VaultCrypto.encryptBundle recovery context bundle
    let second = VaultCrypto.encryptBundle recovery context bundle
    (first = second) |> shouldEqual false
    VaultCrypto.decryptBundle recovery context first |> shouldEqual bundle
    Assert.Throws<System.Security.Cryptography.AuthenticationTagMismatchException>(fun () ->
        VaultCrypto.decryptBundle recovery (context + "!") first |> ignore) |> ignore

[<Test>]
let ``browser bundles and login handoffs open with native cryptography`` () =
    let path = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestFiles", "browser-identity.fixture.json")
    use document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path))
    let fixture = document.RootElement
    let text name = fixture.GetProperty(name: string).GetString() |> Option.ofObj |> Option.get
    let handoff = Json.Deserialize<AccountIdentity.Handoff>(fixture.GetProperty("handoff").GetRawText())
    use temporary = VaultCrypto.fromBundle (VaultCrypto.decode (text "temporaryBundle"))
    let bundle = VaultCrypto.decode (text "accountBundle")
    AccountIdentity.openHandoff staging (text "userCode") "fixture-request" temporary handoff |> shouldEqual bundle
    VaultCrypto.decryptBundle (text "recovery") (text "recoveryContext") (text "recoveryBundle") |> shouldEqual bundle
    let reject origin code value =
        Assert.Throws<Errors.TerrabuildException>(fun () ->
            AccountIdentity.openHandoff origin code "fixture-request" temporary value |> ignore) |> ignore
    reject production (text "userCode") handoff
    reject staging "OTHER-CODE" handoff
    reject staging (text "userCode") { handoff with UserId = "another-user" }
    reject staging (text "userCode") { handoff with KeyId = "another-key" }
    use signer = VaultCrypto.fromBundle bundle
    let changedContext = handoff.Context.Replace("fixture-request", "different-request")
    let outer = { handoff with Context = changedContext }
    let outer = { outer with Signature = VaultCrypto.sign signer (outer.Context + "\n" + outer.Ciphertext) }
    reject staging (text "userCode") outer
    use otherTemporary = VaultCrypto.fromBundle (Array.zeroCreate<byte> 64)
    Assert.Throws<Errors.TerrabuildException>(fun () ->
        AccountIdentity.openHandoff staging (text "userCode") "fixture-request" otherTemporary handoff |> ignore) |> ignore
