module Hash
open System.IO
open System.Security.Cryptography
open System


// Match StreamWriter's BOM-free UTF-8 and strict invalid-surrogate handling.
let private utf8 = System.Text.UTF8Encoding(false, true)

let sha256files files =
    // Hash the ordered concatenation of file digests, not the file contents.
    use aggregate = IncrementalHash.CreateHash(HashAlgorithmName.SHA256)
    for file in files do
        use stream = File.OpenRead(file)
        aggregate.AppendData(SHA256.HashData(stream))
    aggregate.GetHashAndReset() |> Convert.ToHexString

let sha256 (s: string) =
    // StreamWriter.Write(null) previously hashed an empty stream.
    let bytes = if obj.ReferenceEquals(s, null) then Array.empty else utf8.GetBytes(s)
    SHA256.HashData(bytes) |> Convert.ToHexString

let sha256file file =
    use stream = File.OpenRead(file)
    SHA256.HashData(stream) |> Convert.ToHexString

let sha256strings (lines: string seq) =
    lines |> String.join "\n" |> sha256

let md5 (s: string) =
    use provider = MD5.Create()
    let inputBytes = System.Text.Encoding.GetEncoding(0).GetBytes(s)
    let hash = inputBytes |> provider.ComputeHash |> Convert.ToHexString
    hash

let guidify (s: string) =
    use provider = MD5.Create()
    let inputBytes = System.Text.Encoding.GetEncoding(0).GetBytes(s)
    let hashBytes = provider.ComputeHash(inputBytes)
    Guid(hashBytes)
