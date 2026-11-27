module internal AccountIdentity

open System
open System.Security.Cryptography
open System.Text.Json
open Errors

let slot (origin: Uri) userId keyId =
    "insights-account/" + CredentialStore.identifier (origin.GetLeftPart(UriPartial.Authority) + "\n" + userId + "\n" + keyId)

type Handoff = {
    Context: string
    Ciphertext: string
    Signature: string
    UserId: string
    KeyId: string
    WrappingKey: string
    SigningKey: string
}

let openHandoff (origin: Uri) userCode requestId (temporary: VaultCrypto.Keys) (handoff: Handoff) =
    if not (VaultCrypto.verify handoff.SigningKey handoff.Signature (handoff.Context + "\n" + handoff.Ciphertext)) then
        raiseInvalidArg "Invalid Insights account handoff signature."
    use document = JsonDocument.Parse(handoff.Context)
    let fields = document.RootElement.EnumerateArray() |> Seq.toArray
    if fields.Length <> 7 then raiseInvalidArg "Invalid Insights account handoff context."
    let field index = fields[index].GetString()
    if field 0 <> "terrabuild-cli-handoff-v1" || field 1 <> origin.GetLeftPart(UriPartial.Authority) ||
       field 2 <> requestId || field 3 <> userCode || field 4 <> VaultCrypto.encode temporary.WrappingPublic ||
       field 5 <> handoff.UserId || field 6 <> handoff.KeyId then
        raiseInvalidArg "Insights account handoff does not match this login request."
    let payload = VaultCrypto.unseal temporary handoff.Ciphertext
    try
        let context = VaultCrypto.utf8 handoff.Context
        if payload.Length <> context.Length + 64 || not (payload.AsSpan(0, context.Length).SequenceEqual(context)) then
            raiseInvalidArg "Encrypted Insights account handoff context does not match."
        let bundle = payload[context.Length..]
        try
            use keys = VaultCrypto.fromBundle bundle
            if VaultCrypto.encode keys.WrappingPublic <> handoff.WrappingKey || VaultCrypto.encode keys.SigningPublic <> handoff.SigningKey then
                raiseInvalidArg "Insights account bundle does not match its registered public keys."
            bundle
        with _ ->
            CryptographicOperations.ZeroMemory(bundle)
            reraise()
    finally CryptographicOperations.ZeroMemory(payload)
