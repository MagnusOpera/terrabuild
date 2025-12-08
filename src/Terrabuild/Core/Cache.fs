module Cache
open System
open System.IO
open Collections
open Serilog

[<RequireQualifiedAccess>]
type Origin =
    | Local
    | Remote

[<RequireQualifiedAccess>]
type OperationSummary = {
    MetaCommand: string
    Command: string
    Arguments: string list
    Container: string option
    StartedAt: DateTime
    EndedAt: DateTime
    Duration: TimeSpan
    Log: string
    ExitCode: int
}

[<RequireQualifiedAccess>]
type TargetSummary = {
    Project: string
    Target: string
    Operations: OperationSummary list list
    Outputs: string option
    IsSuccessful: bool
    StartedAt: DateTime
    EndedAt: DateTime
    Duration: TimeSpan
    Cache: GraphDef.Artifacts
}


type ArtifactInfo = {
    Path: string
    Size: int
}


type IEntry =
    abstract NextLogFile: unit -> string
    abstract CompleteLogFile: summary:TargetSummary -> unit
    abstract Outputs: string with get
    abstract Logs: string with get
    abstract Complete: summary:TargetSummary -> string list

type ICache =
    abstract TryGetSummaryOnly: useRemote:bool -> id:string -> (Origin * TargetSummary) option
    abstract TryGetSummary: useRemote:bool -> id:string -> TargetSummary option
    abstract GetEntry: useRemote:bool -> id:string -> IEntry


let private summaryFilename = "summary.json"

let private originFilename = "origin"

let createTerrabuildProfile() =
    let tbDir = FS.combinePath ("HOME" |> Environment.envVar |> Option.get) ".terrabuild"
    IO.createDirectory tbDir
    tbDir

let createCache() =
    let cacheDir = FS.combinePath (createTerrabuildProfile()) "cache"
    IO.createDirectory cacheDir
    cacheDir

let createHome() =
    let cacheDir = FS.combinePath (createTerrabuildProfile()) "home"
    IO.createDirectory cacheDir
    cacheDir

let createTmp() =
    let cacheDir = FS.combinePath (createTerrabuildProfile()) "tmp"
    IO.createDirectory cacheDir
    cacheDir

let private setOrigin (origin: Origin) entryDir =
    let originFile = FS.combinePath entryDir originFilename
    origin |> Json.Serialize |> IO.writeTextFile originFile

let private getOrigin entryDir =
    let originFile = FS.combinePath entryDir originFilename
    originFile |> IO.readTextFile |> Json.Deserialize<Origin>

let clearCache () =
    IO.deleteAny (createCache())
    IO.deleteAny (createTmp())

let clearHomeCache () =
    IO.deleteAny (createHome())

let createDirectories() =
    createTerrabuildProfile() |> ignore
    createCache() |> ignore
    createHome() |> ignore
    createTmp() |> ignore


type NewEntry(entryDir: string, useRemote: bool, id: string, storage: Contracts.IStorage, masterKey: byte[] option) =
    let logsDir = FS.combinePath entryDir "logs"
    let outputsDir = FS.combinePath entryDir "outputs"
    let mutable logNum = 1

    do
        match entryDir with
        | FS.Directory _ | FS.File _ -> IO.deleteAny entryDir
        | FS.None _ -> ()

        IO.createDirectory entryDir
        IO.createDirectory logsDir
        // NOTE: outputs is created on demand only

    let write (summary: TargetSummary) file =
        let summary =
            { summary
                with Operations = summary.Operations
                             |> List.map (fun stepGroup ->
                                stepGroup
                                |> List.map (fun step -> { step
                                                            with Log = IO.getFilename step.Log }))
                     Outputs = summary.Outputs
                               |> Option.map (fun outputs -> IO.getFilename outputs) }

        summary |> Json.Serialize |> IO.writeTextFile file

    interface IEntry with

        member _.NextLogFile () =
            let rec nextLogFile() =
                let filename = FS.combinePath logsDir $"step{logNum}.log"
                if IO.exists filename then
                    logNum <- logNum + 1
                    nextLogFile()
                else
                    filename
            nextLogFile()

        member _.CompleteLogFile summary =
            FS.combinePath logsDir $"step{logNum}.json" |> write summary

        member _.Outputs = outputsDir

        member _.Logs = logsDir

        member _.Complete summary =
            let files =
                let uploadDir sourceDir name =
                    let mutable tarFile: string | null = null
                    let mutable compressFile: string | null = null
                    let mutable encryptedFile: string | null = null
                    try
                        let path = $"{id}/{name}"
                        tarFile <- Compression.tar sourceDir
                        compressFile <- Compression.compress (tarFile |> nonNull)
                        encryptedFile <- Encryption.encrypt masterKey id (compressFile |> nonNull)
                        storage.Upload path (encryptedFile |> nonNull)
                    finally
                        IO.deleteAny encryptedFile
                        IO.deleteAny compressFile
                        IO.deleteAny tarFile

                let genFinalSummary() =
                    let rec collect logNum =
                        seq {
                            let filename = FS.combinePath logsDir $"step{logNum}.json"
                            if IO.exists filename then
                                let json = IO.readTextFile filename
                                json |> Json.Deserialize<TargetSummary>
                                yield! collect (logNum+1)
                            else
                                summary
                        }

                    let finalSummary =
                        collect 1
                        |> Seq.reduce (fun s1 s2 -> { s1 with
                                                            Operations = s1.Operations @ s2.Operations
                                                            EndedAt = s2.EndedAt
                                                            Duration = s1.Duration + s2.Duration })
                    FS.combinePath logsDir "summary.json" |> write finalSummary

                if useRemote then
                    let files = [
                        if Directory.Exists outputsDir then uploadDir outputsDir "outputs"
                        genFinalSummary()
                        uploadDir logsDir "logs"
                    ]
                    files
                else
                    genFinalSummary()
                    []

            entryDir |> setOrigin Origin.Local
            files


type Cache(storage: Contracts.IStorage, masterKey: byte[] option) =
    // if there is a entry we already tried to download the summary (result is the value)
    // if not we have never tried to download the summary
    let cachedSummaries = System.Collections.Concurrent.ConcurrentDictionary<string, (Origin*TargetSummary) option>()

    let tryDownload targetDir id name =
        match storage.TryDownload $"{id}/{name}" with
        | Some file ->
            let mutable decryptedFile: string option = None
            let mutable decompressedFile: string | null = null
            try
                decryptedFile <- Encryption.tryDecrypt masterKey id file
                match decryptedFile with
                | Some decryptedFile ->
                    decompressedFile <- Compression.uncompress decryptedFile
                    Compression.untar targetDir (decompressedFile |> nonNull)
                    true
                | _ ->
                    false
            finally
                IO.deleteAny decompressedFile
                IO.deleteAny file
        | _ ->
            false

    let tryLoadSummary logsDir outputsDir summaryFile =
        try
            let summary  = summaryFile |> IO.readTextFile |> Json.Deserialize<TargetSummary>
            let summary = { summary with
                                Operations = summary.Operations
                                        |> List.map (fun stepGroup ->
                                            stepGroup
                                            |> List.map (fun stepLog -> { stepLog with
                                                                            Log = FS.combinePath logsDir stepLog.Log }))
                                Outputs = summary.Outputs |> Option.map (fun _ -> outputsDir) }
            Some summary
        with
            | exn ->
                Log.Error(exn, "Failed to process content {summaryFile}", summaryFile)
                None

    interface ICache with
        // NOTE: do not use when building - only use for graph building
        member _.TryGetSummaryOnly useRemote id : (Origin * TargetSummary) option =
            match cachedSummaries.TryGetValue(id) with
            | true, originSummary -> originSummary
            | false, _ ->
                let entryDir = FS.combinePath (createCache()) id
                let logsDir = FS.combinePath entryDir "logs"
                let outputsDir = FS.combinePath entryDir "outputs"
                let summaryFile = FS.combinePath logsDir summaryFilename
                let completeFile = FS.combinePath entryDir originFilename

                // do we have the summary in local cache?
                match completeFile with
                | FS.File _ ->
                    match tryLoadSummary logsDir outputsDir summaryFile with
                    | Some summary ->
                        let origin = getOrigin entryDir
                        cachedSummaries.TryAdd(id, Some (origin, summary)) |> ignore
                        Some (origin, summary)
                    | _ -> None
                | _ ->
                    if useRemote then
                        if tryDownload logsDir id "logs" then
                            match tryLoadSummary logsDir outputsDir summaryFile with
                            | Some summary ->
                                cachedSummaries.TryAdd(id, Some (Origin.Remote, summary)) |> ignore
                                Some (Origin.Remote, summary)
                            | _ -> None
                        else
                            cachedSummaries.TryAdd(id, None) |> ignore
                            None
                    else
                        None

        member _.TryGetSummary useRemote id : TargetSummary option =
            let entryDir = FS.combinePath (createCache()) id
            let logsDir = FS.combinePath entryDir "logs"
            let outputsDir = FS.combinePath entryDir "outputs"
            let summaryFile = FS.combinePath logsDir summaryFilename
            let completeFile = FS.combinePath entryDir originFilename

            match completeFile with
            | FS.File _ ->
                tryLoadSummary logsDir outputsDir summaryFile
            | _ ->
                if useRemote then
                    if tryDownload logsDir id "logs" then
                        match tryLoadSummary logsDir outputsDir summaryFile with
                        | Some summary ->
                            match summary.Outputs with
                            | Some _ ->
                                if tryDownload outputsDir id "outputs" then
                                    entryDir |> setOrigin Origin.Remote
                                    Some summary
                                else
                                    None
                            | _ ->
                                entryDir |> setOrigin Origin.Remote
                                Some summary
                        | _ -> None
                    else
                        None
                else
                    None

        member _.GetEntry useRemote id : IEntry =
            // invalidate cache as we are creating a new entry
            cachedSummaries.TryRemove(id) |> ignore
            let entryDir = FS.combinePath (createCache()) id
            NewEntry(entryDir, useRemote, id, storage, masterKey)
