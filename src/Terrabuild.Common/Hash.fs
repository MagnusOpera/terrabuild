module Hash
open System.IO
open System.Security.Cryptography
open System


let sha256files files =
    use ms = new MemoryStream()

    let computeFileSha file =
        let sha256 = SHA256.Create()
        use hFile = File.OpenRead(file)
        hFile |> sha256.ComputeHash |> ms.Write

    files |> Seq.iter computeFileSha

    ms.Position <- 0
    let sha256 = SHA256.Create()
    let hash = ms |> sha256.ComputeHash |> Convert.ToHexString
    hash


let sha256 (s: string) =
    let sha256 = SHA256.Create()
    use ms = new MemoryStream()
    use txtWriter = new StreamWriter(ms)
    txtWriter.Write(s)
    txtWriter.Flush()
    ms.Position <- 0L
    let hash = ms |> sha256.ComputeHash |> Convert.ToHexString
    hash

let sha256file file =
    use sha256 = SHA256.Create()
    use stream = File.OpenRead(file)
    stream |> sha256.ComputeHash |> Convert.ToHexString

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
