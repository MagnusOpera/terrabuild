module Tests.Hash
open Hash
open FsUnit
open NUnit.Framework

[<Test>]
let ``verify sha256``() =
    "toto\ntiti"
    |> sha256
    |> String.toLower
    |> should equal "539682fb0b380dbbf3d7085a22055092ba6291638a576eea139cf4b400377015"

[<Test>]
let ``verify sha256 list``() =
    [ "toto"; "titi" ]
    |> sha256strings
    |> String.toLower
    |> should equal "539682fb0b380dbbf3d7085a22055092ba6291638a576eea139cf4b400377015"

[<Test>]
let ``verify sha256 files``() =
    [ "TestFiles/toto.txt"; "TestFiles/titi.txt" ]
    |> sha256files
    |> String.toLower
    |> should equal "ce8a6882216d40d8a9245067c51fd54dd40e9363001b4b9658197a71eb2250cb"

// Independent legacy implementations protect persistent cache keys, including
// encoding details, file order, duplicate files and newline joining.
let private legacyStringHash (value: string) =
    use stream = new System.IO.MemoryStream()
    use writer = new System.IO.StreamWriter(stream)
    writer.Write(value)
    writer.Flush()
    stream.Position <- 0L
    use algorithm = System.Security.Cryptography.SHA256.Create()
    algorithm.ComputeHash(stream) |> System.Convert.ToHexString

[<Test>]
let ``string hashes preserve legacy encoding and joining`` () =
    for value in [ Unchecked.defaultof<string>; ""; "\uFEFF"; "é😀漢字"; "a\r\nb\nc"; String.replicate 10000 "é😀" ] do
        sha256 value |> should equal (legacyStringHash value)
    for lines in [ []; [ "" ]; [ "a"; "b" ]; [ "a\nb" ]; [ "é"; "😀" ] ] do
        sha256strings lines |> should equal (legacyStringHash (System.String.Join("\n", lines)))
    let invalid = System.String([| char 0xD800 |])
    Assert.Throws<System.Text.EncoderFallbackException>(fun () -> legacyStringHash invalid |> ignore) |> ignore
    Assert.Throws<System.Text.EncoderFallbackException>(fun () -> sha256 invalid |> ignore) |> ignore

[<Test>]
let ``file hashes preserve ordered legacy aggregation`` () =
    let root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid().ToString("N"))
    System.IO.Directory.CreateDirectory(root) |> ignore
    try
        let files =
            [ [||]; [| 0uy; 255uy; 10uy |]; Array.init 100000 (fun i -> byte (i % 256)) ]
            |> List.mapi (fun i bytes ->
                let path = System.IO.Path.Combine(root, string i)
                System.IO.File.WriteAllBytes(path, bytes)
                path)
        let legacy files =
            use stream = new System.IO.MemoryStream()
            for file in files do
                use algorithm = System.Security.Cryptography.SHA256.Create()
                use input = System.IO.File.OpenRead(file)
                let digest = algorithm.ComputeHash(input)
                sha256file file |> should equal (System.Convert.ToHexString(digest))
                stream.Write(digest)
            stream.Position <- 0L
            use aggregate = System.Security.Cryptography.SHA256.Create()
            aggregate.ComputeHash(stream) |> System.Convert.ToHexString
        for ordered in [ []; files; List.rev files; files @ files; [files[1]] ] do
            sha256files ordered |> should equal (legacy ordered)
    finally
        System.IO.Directory.Delete(root, true)
