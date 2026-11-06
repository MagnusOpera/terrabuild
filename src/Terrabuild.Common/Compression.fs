module Compression
open System.IO.Compression
open System.IO
open System.Formats.Tar
open System

let tar (inputDirectoryName: string) =
    let outputFileName = IO.getTempFilename()
    use output = File.Create(outputFileName)
    use writer = new TarWriter(output, false)
    let root = Path.GetFullPath(inputDirectoryName)
    let enumeration = EnumerationOptions(AttributesToSkip = enum<FileAttributes> 0, IgnoreInaccessible = false)
    let rec writeDirectory directory =
        for entry in Directory.EnumerateFileSystemEntries(directory, "*", enumeration) |> Seq.sort do
            let relative = Path.GetRelativePath(root, entry).Replace(Path.DirectorySeparatorChar, '/')
            writer.WriteEntry(entry, relative)
            if Directory.Exists(entry) && isNull (DirectoryInfo(entry).LinkTarget) then
                writeDirectory entry
    writeDirectory root
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
    let root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputDirectoryName))
    let links = ResizeArray<string * string>()
    let rejectLinkedDestination destination =
        let mutable current = destination
        while current <> root do
            if not (isNull (FileInfo(current).LinkTarget)) then
                invalidArg (nameof inputFileName) $"Tar destination '{destination}' traverses a symbolic link."
            current <- Path.GetDirectoryName(current) |> nonNull
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
                rejectLinkedDestination destinationPath
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
                | TarEntryType.SymbolicLink ->
                    let linkTarget = entry.LinkName
                    if String.IsNullOrWhiteSpace(linkTarget) || Path.IsPathRooted(linkTarget) then
                        invalidArg (nameof inputFileName) "Tar symbolic links must use relative targets."
                    let parent = Path.GetDirectoryName(destinationPath) |> nonNull
                    let resolved = Path.GetFullPath(Path.Combine(parent, linkTarget.Replace('\\', '/')))
                    let relative = Path.GetRelativePath(root, resolved)
                    if relative = ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathRooted(relative) then
                        invalidArg (nameof inputFileName) "Tar symbolic link target escapes its extraction directory."
                    links.Add(destinationPath, linkTarget)
                | _ -> ()

                extractNext ()

    extractNext ()
    // Create links last so archive files cannot be written through an earlier
    // link. A link that collides with an extracted directory/file is invalid.
    for destination, linkTarget in links do
        rejectLinkedDestination destination
        let parent = Path.GetDirectoryName(destination) |> nonNull
        let resolved = Path.GetFullPath(Path.Combine(parent, linkTarget))
        if Directory.Exists(resolved) then
            invalidArg (nameof inputFileName) "Directory symbolic links are not supported in remote cache snapshots."
        if Path.Exists(destination) then
            invalidArg (nameof inputFileName) $"Tar symbolic link '{destination}' replaces an existing entry."
        Path.GetDirectoryName(destination) |> nonNull |> Directory.CreateDirectory |> ignore
        File.CreateSymbolicLink(destination, linkTarget) |> ignore

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
