module Encryption
open System.Security.Cryptography
open System.Text
open System.IO
open System


module private SecureArchive =

    let masterKeyFromString (masterKeyString: string) : byte[] =
        let salt = Encoding.UTF8.GetBytes "Terrabuild.MasterKey.Salt.v1"
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
            "terrabuild-artifact:v1:" + artifactId
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
    let private MAGIC_TAG = "TBARCH01"

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

    let encryptFileArchive (encKey: byte[]) (macKey: byte[]) (inputPath: string) (outputPath: string) =
        // 1. Encrypt (streaming) to output (without MAC)
        use input = File.OpenRead inputPath
        use output = File.Open(outputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None)

        let magic = Encoding.ASCII.GetBytes MAGIC_TAG
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


    let decryptFileArchive (encKey: byte[]) (macKey: byte[]) (inputPath: string) (outputPath: string) =
        use input = File.OpenRead inputPath

        // Basic length check: must at least fit magic + iv + tag
        if input.Length < 8L + 16L + 32L then
            invalidOp "Invalid encrypted artifact (too short)"

        // 1. Verify HMAC
        let totalLen = input.Length
        let tagLen = 32
        let dataLen = totalLen - int64 tagLen

        use hmac = new HMACSHA256(macKey)
        let buf = Array.zeroCreate<byte> 8192

        input.Position <- 0L
        let mutable remaining = dataLen
        while remaining > 0L do
            let toRead = int (min (int64 buf.Length) remaining)
            let read = input.Read(buf, 0, toRead)
            if read <= 0 then invalidOp "Unexpected EOF while verifying MAC"
            hmac.TransformBlock(buf, 0, read, null, 0) |> ignore
            remaining <- remaining - int64 read

        hmac.TransformFinalBlock(Array.empty, 0, 0) |> ignore
        let computedTag = hmac.Hash |> nonNull

        // Read stored tag
        let storedTag = Array.zeroCreate<byte> tagLen
        let readTag = input.Read(storedTag, 0, tagLen)
        if readTag <> tagLen then invalidOp "Failed to read tag"

        // Constant-time compare
        let mutable diff = 0
        for i in 0 .. tagLen - 1 do
            diff <- diff ||| (int computedTag[i] ^^^ int storedTag[i])

        if diff <> 0 then
            invalidOp "MAC verification failed"

        // 2. Decrypt (second streaming pass)
        use input2 = File.OpenRead inputPath
        use output = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None)

        let magic = Encoding.ASCII.GetBytes MAGIC_TAG
        let headerMagic = Array.zeroCreate<byte> magic.Length
        input2.Read(headerMagic, 0, headerMagic.Length) |> ignore
        if not (System.Linq.Enumerable.SequenceEqual(headerMagic, magic)) then
            invalidOp "Not a Terrabuild encrypted artifact"

        let iv = Array.zeroCreate<byte> 16
        input2.Read(iv, 0, iv.Length) |> ignore

        use aes = Aes.Create()
        aes.Key <- encKey
        aes.IV <- iv
        aes.Mode <- CipherMode.CBC
        aes.Padding <- PaddingMode.PKCS7

        // Limit decryption to [dataLen - (magic + iv)] bytes (exclude tag)
        let cipherLen = dataLen - int64 (magic.Length + iv.Length)

        use cryptoStream = new CryptoStream(output, aes.CreateDecryptor(), CryptoStreamMode.Write)

        let buf2 = Array.zeroCreate<byte> 8192
        let mutable remaining2 = cipherLen
        while remaining2 > 0L do
            let toRead = int (min (int64 buf2.Length) remaining2)
            let read = input2.Read(buf2, 0, toRead)
            if read <= 0 then invalidOp "Unexpected EOF while decrypting"
            cryptoStream.Write(buf2, 0, read)
            remaining2 <- remaining2 - int64 read

        cryptoStream.FlushFinalBlock()


let isEncrypted file = SecureArchive.isEncryptedArchive file

let masterKeyFromString masterKeyString = SecureArchive.masterKeyFromString masterKeyString

let encrypt masterKey artifactId inputFile =
    match masterKey with
    | Some masterKey ->
        let outputFile = IO.getTempFilename()
        let encKey, macKey = SecureArchive.deriveKeys masterKey artifactId
        SecureArchive.encryptFileArchive encKey macKey inputFile outputFile
        outputFile
    | _ -> inputFile
 
let tryDecrypt masterKey artifactId inputFile =
    match masterKey with
    | Some masterKey ->
        try
            let outputFile = IO.getTempFilename()
            let encKey, macKey = SecureArchive.deriveKeys masterKey artifactId
            SecureArchive.decryptFileArchive encKey macKey inputFile outputFile
            Some outputFile
        with _ ->
            None
    | _ ->
        if isEncrypted inputFile then None
        else Some inputFile
