module Encryption
open System.Security.Cryptography
open System.Text
open System.IO
open System


module private SecureArchive =

    let masterKeyFromString (salt: string) (masterKeyString: string) : byte[] =
        let salt = Encoding.UTF8.GetBytes salt
        let iterations = 100_000

        let key =
            Rfc2898DeriveBytes.Pbkdf2(
                password = masterKeyString,
                salt = salt,
                iterations = iterations,
                hashAlgorithm = HashAlgorithmName.SHA256,
                outputLength = 32
            )
        key

    let deriveBaseKey (masterKey: byte[]) (artifactId: string) =
        use hmac = new HMACSHA256(masterKey)
        let material =
            "terrabuild-archive:v1:" + artifactId
            |> Encoding.UTF8.GetBytes
        hmac.ComputeHash(material) // 32 bytes

    let deriveKeys (masterKey: byte[]) (artifactId: string) =
        let baseKey = deriveBaseKey masterKey artifactId
        use hmac = new HMACSHA256(baseKey)
        let encKey = hmac.ComputeHash(Encoding.UTF8.GetBytes "enc")  // 32 bytes
        let macKey = hmac.ComputeHash(Encoding.UTF8.GetBytes "mac")  // 32 bytes
        encKey, macKey


    // [ magic "TBARCH01" (8 bytes) ]
    // [ iv (16 bytes) ]
    // [ ciphertext (streamed) ]
    // [ tag (HMAC-SHA256, 32 bytes) ]

    [<Literal>]
    let MAGIC_TAG = "TBARCH01"

    let isEncryptedArchive (filePath: string) =
        if not (File.Exists filePath) then
            false
        else
            try
                use fs = File.OpenRead filePath
                let magic = Encoding.ASCII.GetBytes MAGIC_TAG
                let buffer = Array.zeroCreate<byte> magic.Length

                let read = fs.Read(buffer, 0, buffer.Length)
                read = magic.Length && System.Linq.Enumerable.SequenceEqual(buffer, magic)
            with
            | :? IOException
            | :? UnauthorizedAccessException ->
                false

    let encryptFileArchive (encKey: byte[]) (macKey: byte[]) (magicTag: string) (inputPath: string) (outputPath: string) =
        // 1. Encrypt (streaming) to output (without MAC)
        use input = File.OpenRead inputPath
        use output = File.Open(outputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None)

        let magic = Encoding.ASCII.GetBytes magicTag
        output.Write(magic, 0, magic.Length)

        let iv = RandomNumberGenerator.GetBytes 16
        output.Write(iv, 0, iv.Length)

        use aes = Aes.Create()
        aes.Key <- encKey
        aes.IV <- iv
        aes.Mode <- CipherMode.CBC
        aes.Padding <- PaddingMode.PKCS7

        use cryptoStream = new CryptoStream(output, aes.CreateEncryptor(), CryptoStreamMode.Write)

        let buffer = Array.zeroCreate<byte> 8192
        let mutable read = input.Read(buffer, 0, buffer.Length)
        while read > 0 do
            cryptoStream.Write(buffer, 0, read)
            read <- input.Read(buffer, 0, buffer.Length)

        cryptoStream.FlushFinalBlock()

        // 2. Compute HMAC over [magic || iv || ciphertext] streaming, then append tag
        output.Flush()
        output.Position <- 0L

        use hmac = new HMACSHA256(macKey)
        let hbuf = Array.zeroCreate<byte> 8192
        let mutable hread = output.Read(hbuf, 0, hbuf.Length)
        while hread > 0 do
            hmac.TransformBlock(hbuf, 0, hread, null, 0) |> ignore
            hread <- output.Read(hbuf, 0, hbuf.Length)

        hmac.TransformFinalBlock(Array.empty, 0, 0) |> ignore
        let tag = hmac.Hash |> nonNull

        output.Position <- output.Length
        output.Write(tag, 0, tag.Length)
        output.Flush()


    let decryptFileArchive (encKey: byte[]) (macKey: byte[]) (magicTag: string) (expectedDigest: byte[] option) (inputPath: string) (outputPath: string) =
        use input = File.Open(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read)
        let magic = Encoding.ASCII.GetBytes magicTag
        let headerSize = int64 magic.Length + 16L
        let tagSize = 32L
        let ciphertextSize = input.Length - headerSize - tagSize
        if ciphertextSize < 16L || ciphertextSize % 16L <> 0L then
            invalidOp "Invalid encrypted archive length"

        let header = Array.zeroCreate<byte> (int headerSize)
        input.ReadExactly(header)
        if not (header.AsSpan(0, magic.Length).SequenceEqual(magic)) then
            invalidOp "Not a supported Terrabuild encrypted archive"

        use mac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, macKey)
        use digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256)
        mac.AppendData(header)
        digest.AppendData(header)
        let buffer = Array.zeroCreate<byte> 8192
        let mutable remaining = ciphertextSize
        while remaining > 0L do
            let count = int (min (int64 buffer.Length) remaining)
            input.ReadExactly(buffer, 0, count)
            mac.AppendData(buffer, 0, count)
            digest.AppendData(buffer, 0, count)
            remaining <- remaining - int64 count

        let tag = Array.zeroCreate<byte> (int tagSize)
        input.ReadExactly(tag)
        if not (CryptographicOperations.FixedTimeEquals(tag, mac.GetHashAndReset())) then
            invalidOp "MAC verification failed"
        digest.AppendData(tag)
        match expectedDigest with
        | Some expected when not (CryptographicOperations.FixedTimeEquals(expected, digest.GetHashAndReset())) ->
            invalidOp "Ciphertext digest verification failed"
        | _ -> ()

        // Read the same file handle that was authenticated before opening any plaintext output.
        input.Position <- headerSize
        use output = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None)
        use aes = Aes.Create()
        aes.Key <- encKey
        aes.IV <- header[magic.Length..]
        aes.Mode <- CipherMode.CBC
        aes.Padding <- PaddingMode.PKCS7
        use cryptoStream = new CryptoStream(output, aes.CreateDecryptor(), CryptoStreamMode.Write)
        remaining <- ciphertextSize
        while remaining > 0L do
            let count = int (min (int64 buffer.Length) remaining)
            input.ReadExactly(buffer, 0, count)
            cryptoStream.Write(buffer, 0, count)
            remaining <- remaining - int64 count
        cryptoStream.FlushFinalBlock()


let isEncrypted file = SecureArchive.isEncryptedArchive file

let masterKeyFromString masterKeyString = SecureArchive.masterKeyFromString masterKeyString

let encrypt masterKey artifactId inputFile =
    match masterKey with
    | Some masterKey ->
        let outputFile = IO.getTempFilename()
        let encKey, macKey = SecureArchive.deriveKeys masterKey artifactId
        SecureArchive.encryptFileArchive encKey macKey SecureArchive.MAGIC_TAG inputFile outputFile
        outputFile
    | _ -> inputFile
 
let tryDecrypt masterKey artifactId inputFile =
    match masterKey with
    | Some masterKey ->
        let mutable outputFile = None
        try
            let file = IO.getTempFilename()
            outputFile <- Some file
            let encKey, macKey = SecureArchive.deriveKeys masterKey artifactId
            SecureArchive.decryptFileArchive encKey macKey SecureArchive.MAGIC_TAG None inputFile file
            Some file
        with _ ->
            outputFile |> Option.iter IO.deleteAny
            None
    | _ ->
        if isEncrypted inputFile then None
        else Some inputFile

// A binary-safe context supplied by the workspace protocol separates the keys
// for each publication and payload while keeping the streaming archive cipher.
let private workspaceMagic = "TBARCH02"

let private workspaceKeys root context =
    SecureArchive.deriveKeys root ("workspace:v2:" + context)

let encryptWorkspace root context inputFile =
    let output = IO.getTempFilename()
    let encryptionKey, macKey = workspaceKeys root context
    try
        try
            SecureArchive.encryptFileArchive encryptionKey macKey workspaceMagic inputFile output
            output
        with _ ->
            IO.deleteAny output
            reraise()
    finally
        CryptographicOperations.ZeroMemory(encryptionKey)
        CryptographicOperations.ZeroMemory(macKey)

let decryptWorkspace root context expectedDigest inputFile outputFile =
    let encryptionKey, macKey = workspaceKeys root context
    try
        try
            SecureArchive.decryptFileArchive encryptionKey macKey workspaceMagic (Some expectedDigest) inputFile outputFile
        with _ ->
            IO.deleteAny outputFile
            reraise()
    finally
        CryptographicOperations.ZeroMemory(encryptionKey)
        CryptographicOperations.ZeroMemory(macKey)
