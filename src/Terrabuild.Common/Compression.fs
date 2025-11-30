module Compression
open System.IO.Compression
open System.IO
open System.Formats.Tar

let tar (inputDirectoryName: string) =
    let outputFileName = IO.getTempFilename()
    use output = File.Create(outputFileName)
    TarFile.CreateFromDirectory(inputDirectoryName, output, false)
    outputFileName

let untar (outputDirectoryName: string) (inputFileName: string) =
    IO.createDirectory outputDirectoryName
    TarFile.ExtractToDirectory(inputFileName, outputDirectoryName, true)

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
