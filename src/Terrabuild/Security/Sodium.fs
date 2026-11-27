module internal Terrabuild.Security.Sodium

open System
open System.Runtime.InteropServices
open System.Security.Cryptography

[<Literal>]
let private Library = "libsodium"

module private Native =
    [<DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)>]
    extern int sodium_init()

    [<DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)>]
    extern int crypto_box_seed_keypair([<Out>] byte[] publicKey, [<Out>] byte[] privateKey, byte[] seed)

    [<DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)>]
    extern int crypto_sign_seed_keypair([<Out>] byte[] publicKey, [<Out>] byte[] privateKey, byte[] seed)

    [<DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)>]
    extern int crypto_sign_detached([<Out>] byte[] signature, nativeint signatureLength, byte[] message, uint64 messageLength, byte[] privateKey)

    [<DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)>]
    extern int crypto_sign_verify_detached(byte[] signature, byte[] message, uint64 messageLength, byte[] publicKey)

    [<DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)>]
    extern int crypto_scalarmult([<Out>] byte[] secret, byte[] privateKey, byte[] publicKey)

    [<DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)>]
    extern int crypto_box_seal([<Out>] byte[] ciphertext, byte[] plaintext, uint64 plaintextLength, byte[] publicKey)

    [<DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)>]
    extern int crypto_box_seal_open([<Out>] byte[] plaintext, byte[] ciphertext, uint64 ciphertextLength, byte[] publicKey, byte[] privateKey)

let private initialized =
    lazy (
        try
            if Native.sodium_init() < 0 then
                Errors.raiseExternalError "Could not initialize libsodium."
        with
        | :? DllNotFoundException
        | :? EntryPointNotFoundException
        | :? BadImageFormatException ->
            Errors.raiseExternalError "Could not load the libsodium library for this Terrabuild installation.")

let private keySize = 32
let private signingPrivateKeySize = 64
let private signatureSize = 64
let private sealOverhead = 48

let private requireLength name expected (buffer: byte[]) =
    if buffer.Length <> expected then invalidArg name $"Expected {expected} bytes."

let private check result message =
    if result <> 0 then raise (CryptographicException(message: string))

let private keyPair generate privateKeySize (seed: byte[]) =
    requireLength (nameof seed) keySize seed
    initialized.Force()
    let publicKey = Array.zeroCreate<byte> keySize
    let privateKey = Array.zeroCreate<byte> privateKeySize
    try
        check (generate publicKey privateKey seed) "Could not derive the libsodium key pair."
        publicKey, privateKey
    with _ ->
        CryptographicOperations.ZeroMemory(privateKey)
        reraise()

let wrappingKeyPair seed =
    keyPair (fun pk sk seed -> Native.crypto_box_seed_keypair(pk, sk, seed)) keySize seed

let signingKeyPair seed =
    keyPair (fun pk sk seed -> Native.crypto_sign_seed_keypair(pk, sk, seed)) signingPrivateKeySize seed

let sign privateKey (message: byte[]) =
    requireLength (nameof privateKey) signingPrivateKeySize privateKey
    initialized.Force()
    let signature = Array.zeroCreate<byte> signatureSize
    check (Native.crypto_sign_detached(signature, 0n, message, uint64 message.Length, privateKey)) "Could not sign with Ed25519."
    signature

let verify (publicKey: byte[]) (signature: byte[]) (message: byte[]) =
    if publicKey.Length <> keySize || signature.Length <> signatureSize then false
    else
        initialized.Force()
        Native.crypto_sign_verify_detached(signature, message, uint64 message.Length, publicKey) = 0

let sharedSecret privateKey publicKey =
    requireLength (nameof privateKey) keySize privateKey
    requireLength (nameof publicKey) keySize publicKey
    initialized.Force()
    let secret = Array.zeroCreate<byte> keySize
    try
        check (Native.crypto_scalarmult(secret, privateKey, publicKey)) "Invalid X25519 peer key."
        secret
    with _ ->
        CryptographicOperations.ZeroMemory(secret)
        reraise()

let seal publicKey (plaintext: byte[]) =
    requireLength (nameof publicKey) keySize publicKey
    initialized.Force()
    let ciphertext = Array.zeroCreate<byte> (Checked.(+) plaintext.Length sealOverhead)
    check (Native.crypto_box_seal(ciphertext, plaintext, uint64 plaintext.Length, publicKey)) "Could not seal the workspace key."
    ciphertext

let unseal publicKey privateKey (ciphertext: byte[]) =
    requireLength (nameof publicKey) keySize publicKey
    requireLength (nameof privateKey) keySize privateKey
    if ciphertext.Length < sealOverhead then
        raise (CryptographicException("Invalid workspace key envelope length."))
    initialized.Force()
    let plaintext = Array.zeroCreate<byte> (ciphertext.Length - sealOverhead)
    try
        check (Native.crypto_box_seal_open(plaintext, ciphertext, uint64 ciphertext.Length, publicKey, privateKey)) "Could not authenticate the workspace key envelope."
        plaintext
    with _ ->
        CryptographicOperations.ZeroMemory(plaintext)
        reraise()
