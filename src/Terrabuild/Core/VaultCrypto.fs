module internal VaultCrypto

open System
open System.Security.Cryptography
open System.Text
open Terrabuild.Security

let encode (value: byte[]) =
    Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_')

let decode (value: string) =
    let padded = value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '=')
    let bytes = Convert.FromBase64String(padded)
    if encode bytes <> value then invalidArg "value" "Invalid base64url."
    bytes

let utf8 (value: string) = Encoding.UTF8.GetBytes value

let hash (value: string) =
    value |> utf8 |> SHA256.HashData |> Convert.ToHexStringLower

let derive (secret: byte[]) info =
    HKDF.DeriveKey(HashAlgorithmName.SHA256, secret, 32, Array.empty, utf8 info)

let identifier () = Guid.NewGuid().ToString("N")

type Keys = {
    WrappingPublic: byte[]
    WrappingPrivate: byte[]
    SigningPublic: byte[]
    SigningPrivate: byte[]
} with
    interface IDisposable with
        member this.Dispose() =
            CryptographicOperations.ZeroMemory(this.WrappingPrivate)
            CryptographicOperations.ZeroMemory(this.SigningPrivate)

let private fromSeeds (wrapping: byte[]) (signing: byte[]) =
    let wrappingPublic, wrappingPrivate = Sodium.wrappingKeyPair wrapping
    try
        let signingPublic, signingPrivate = Sodium.signingKeyPair signing
        { WrappingPublic = wrappingPublic
          WrappingPrivate = wrappingPrivate
          SigningPublic = signingPublic
          SigningPrivate = signingPrivate }
    with _ ->
        CryptographicOperations.ZeroMemory(wrappingPrivate)
        reraise()

let fromBundle (bundle: byte[]) =
    if bundle.Length <> 64 then invalidArg "bundle" "Invalid account key bundle."
    let wrapping, signing = bundle[..31], bundle[32..]
    try fromSeeds wrapping signing
    finally
        CryptographicOperations.ZeroMemory(wrapping)
        CryptographicOperations.ZeroMemory(signing)

let private parseSecret (prefix: string) (value: string) =
    if not (value.StartsWith(prefix, StringComparison.Ordinal)) then
        Errors.raiseInvalidArg "Unrecognized Insights secret format."
    let bytes = decode (value.Substring(prefix.Length))
    if bytes.Length <> 32 then Errors.raiseInvalidArg "Invalid Insights secret length."
    bytes

let private makeSecret prefix =
    let secret = RandomNumberGenerator.GetBytes(32)
    try prefix + encode secret
    finally CryptographicOperations.ZeroMemory(secret)

let newToken () = makeSecret "tbci2_"
let newRecovery () = makeSecret "tbrec1_"

let ciCredentials (origin: Uri) workspace token =
    let seed = parseSecret "tbci2_" token
    let scope = origin.GetLeftPart(UriPartial.Authority) + "\n" + workspace
    let authentication = derive seed ("terrabuild-ci-v2-auth\n" + scope)
    let wrapping = derive seed ("terrabuild-ci-v2-wrapping\n" + scope)
    let signing = derive seed ("terrabuild-ci-v2-signing\n" + scope)
    try encode authentication, fromSeeds wrapping signing
    finally
        for value in [ seed; authentication; wrapping; signing ] do
            CryptographicOperations.ZeroMemory(value)

let sign (keys: Keys) message =
    Sodium.sign keys.SigningPrivate (utf8 message) |> encode

let verify key signature message =
    try
        Sodium.verify (decode key) (decode signature) (utf8 message)
    with
    | :? FormatException
    | :? ArgumentException -> false

let wrappingProof (keys: Keys) serverKey message =
    let shared = Sodium.sharedSecret keys.WrappingPrivate (decode serverKey)
    try
        let key = derive shared "terrabuild-key-registration-v1"
        try HMACSHA256.HashData(key, utf8 message) |> encode
        finally CryptographicOperations.ZeroMemory(key)
    finally CryptographicOperations.ZeroMemory(shared)

let seal recipient (data: byte[]) =
    Sodium.seal (decode recipient) data |> encode

let unseal (keys: Keys) ciphertext =
    Sodium.unseal keys.WrappingPublic keys.WrappingPrivate (decode ciphertext)

let private withRecoveryKey recovery action =
    let secret = parseSecret "tbrec1_" recovery
    let key = derive secret "terrabuild-account-recovery-v1"
    try
        use aes = new AesGcm(key, 16)
        action aes
    finally
        CryptographicOperations.ZeroMemory(secret)
        CryptographicOperations.ZeroMemory(key)

let encryptBundle recovery context (bundle: byte[]) =
    withRecoveryKey recovery (fun aes ->
        let nonce = RandomNumberGenerator.GetBytes(12)
        let tag = Array.zeroCreate<byte> 16
        let ciphertext = Array.zeroCreate<byte> bundle.Length
        aes.Encrypt(nonce, bundle, ciphertext, tag, utf8 context)
        Array.concat [ nonce; tag; ciphertext ] |> encode)

let decryptBundle recovery context ciphertext =
    withRecoveryKey recovery (fun aes ->
        let encrypted = decode ciphertext
        if encrypted.Length <> 92 then invalidArg "ciphertext" "Invalid account key bundle."
        let plaintext = Array.zeroCreate<byte> 64
        aes.Decrypt(encrypted[..11], encrypted[28..], encrypted[12..27], plaintext, utf8 context)
        plaintext)
