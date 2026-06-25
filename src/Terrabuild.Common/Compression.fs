module Compression
open System.IO.Compression
open System.IO
open System.Formats.Tar
open System

let tar (inputDirectoryName: string) =
    let outputFileName = IO.getTempFilename()
    use output = File.Create(outputFileName)
    TarFile.CreateFromDirectory(inputDirectoryName, output, false)
    outputFileName

let private tryGetSafeTarDestination (outputDirectoryName: string) (entryName: string) =
    let normalized =
        entryName.Replace('\\', '/').Trim()

    if String.IsNullOrWhiteSpace normalized || normalized = "." || normalized = "./" then
        None
    else
        let relativePath = normalized.TrimStart('/')
        let rootPath = Path.GetFullPath(outputDirectoryName)
        let destinationPath = Path.GetFullPath(Path.Combine(rootPath, relativePath))
        let relativeDestination = Path.GetRelativePath(rootPath, destinationPath)

        if relativeDestination = ".."
           || relativeDestination.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
           || Path.IsPathRooted(relativeDestination) then
            invalidArg (nameof entryName) $"Tar entry '{entryName}' escapes '{outputDirectoryName}'."

        Some destinationPath

let untar (outputDirectoryName: string) (inputFileName: string) =
    IO.createDirectory outputDirectoryName
    use input = File.OpenRead(inputFileName)
    use reader = new TarReader(input, false)

    let rec extractNext () =
        match reader.GetNextEntry(false) with
        | null -> ()
        | entry ->
            match tryGetSafeTarDestination outputDirectoryName entry.Name with
            | None ->
                extractNext ()
            | Some destinationPath ->
                match entry.EntryType with
                | TarEntryType.Directory ->
                    Directory.CreateDirectory(destinationPath) |> ignore
                | TarEntryType.RegularFile
                | TarEntryType.V7RegularFile
                | TarEntryType.ContiguousFile ->
                    match Path.GetDirectoryName(destinationPath) with
                    | null -> ()
                    | parentDirectory -> Directory.CreateDirectory(parentDirectory) |> ignore
                    entry.ExtractToFile(destinationPath, true)
                | _ -> ()

                extractNext ()

    extractNext ()

let compress (inputFileName: string) =
    let outputFileName = IO.getTempFilename()
    use input = File.OpenRead(inputFileName)
    use output = File.Create(outputFileName)
    use compressor = new BrotliStream(output, CompressionMode.Compress)
    input.CopyTo(compressor);
    outputFileName

let uncompress (inputFileName: string) =
    let outputFileName = IO.getTempFilename()
    use input = File.OpenRead(inputFileName)
    use output = File.Create(outputFileName)
    use decompressor = new BrotliStream(input, CompressionMode.Decompress)
    decompressor.CopyTo(output)
    outputFileName
