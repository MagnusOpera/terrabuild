module Cache
open System
open System.IO
open System.Threading
open Collections
open Errors
open Serilog

[<RequireQualifiedAccess>]
type Origin =
    | Local
    | Remote

[<RequireQualifiedAccess>]
type OperationSummary = {
    MetaCommand: string
    Command: string
    Arguments: string
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
    HasOutputs: bool
    IsSuccessful: bool
    StartedAt: DateTime
    EndedAt: DateTime
    Duration: TimeSpan
    Cache: GraphDef.ArtifactMode
}

[<RequireQualifiedAccess>]
type private StoredTargetSummary = {
    Project: string
    Target: string
    Operations: OperationSummary list list
    Outputs: string option
    IsSuccessful: bool
    StartedAt: DateTime
    EndedAt: DateTime
    Duration: TimeSpan
    Cache: GraphDef.ArtifactMode
}


type ArtifactInfo = {
    Path: string
    Size: int
}

[<RequireQualifiedAccess>]
type PruneSummary = {
    Scanned: int
    Pruned: int
    Skipped: int
}


type IEntry =
    abstract NextLogFile: unit -> string
    abstract StoreOutputs: sourceDir:string -> entries:string list -> string option
    abstract StoreLogs: entries:string list -> unit
    abstract Complete: summary:TargetSummary -> string list

type ICache =
    abstract TryGetSummaryOnly: useRemote:bool -> id:string -> (Origin * TargetSummary) option
    abstract CanRestore: useRemote:bool -> id:string -> summary:TargetSummary -> bool
    abstract TryGetSummary: useRemote:bool -> id:string -> TargetSummary option
    abstract Restore: useRemote:bool -> id:string -> outputs:string set -> projectDirectory:string -> TargetSummary option
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

let private touchOrigin entryDir =
    let originFile = FS.combinePath entryDir originFilename
    if File.Exists originFile then
        File.SetLastWriteTimeUtc(originFile, DateTime.UtcNow)

let private withEntryLock entryDir action =
    let parent =
        entryDir
        |> FS.parentDirectory
        |> Option.defaultWith (fun () -> raiseBugError $"Cache entry '{entryDir}' has no parent directory")
    IO.createDirectory parent
    let lockFile = $"{entryDir}.lock"

    let rec acquire () =
        try
            new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)
        with :? IOException ->
            Thread.Sleep(25)
            acquire ()

    use lease = acquire ()
    action ()

let private createStagingDirectory entryDir =
    let parent =
        entryDir
        |> FS.parentDirectory
        |> Option.defaultWith (fun () -> raiseBugError $"Cache entry '{entryDir}' has no parent directory")
    IO.createDirectory parent
    let name = IO.getFilename entryDir
    let stagingDir = FS.combinePath parent $".{name}.tmp-{Guid.NewGuid():N}"
    IO.createDirectory stagingDir
    stagingDir

let private replaceDirectory entryDir stagingDir =
    let backupDir = $"{entryDir}.old-{Guid.NewGuid():N}"
    let hadPrevious = Directory.Exists entryDir
    try
        if hadPrevious then Directory.Move(entryDir, backupDir)
        Directory.Move(stagingDir, entryDir)
        if hadPrevious then IO.deleteAny backupDir
    with _ ->
        if Directory.Exists entryDir then IO.deleteAny entryDir
        if hadPrevious && Directory.Exists backupDir then Directory.Move(backupDir, entryDir)
        reraise()

let private publishDirectory entryDir stagingDir =
    withEntryLock entryDir (fun () -> replaceDirectory entryDir stagingDir)

let private restoreOutputsTransaction entryDir outputs projectDirectory =
    let cachedOutputs = FS.combinePath entryDir "outputs"
    let projectDirectory = Path.GetFullPath(projectDirectory)
    let parent =
        projectDirectory
        |> FS.parentDirectory
        |> Option.defaultWith (fun () -> raiseBugError $"Project directory '{projectDirectory}' has no parent")
    let transactionDir = FS.combinePath parent $".terrabuild-restore-{Guid.NewGuid():N}"
    let stagedDir = FS.combinePath transactionDir "staged"
    let backupDir = FS.combinePath transactionDir "backup"
    let cachedFiles = IO.enumerateFiles cachedOutputs
    let currentFiles = (IO.createSnapshot outputs projectDirectory).TimestampedFiles.Keys |> List.ofSeq

    let moveFiles targetDir baseDir files =
        for file in files do
            let relative = FS.relativePath baseDir file
            let target = FS.combinePath targetDir relative
            target
            |> FS.parentDirectory
            |> Option.iter IO.createDirectory
            File.Move(file, target, true)

    try
        IO.copyFiles stagedDir cachedOutputs cachedFiles |> ignore
        IO.copyFiles backupDir projectDirectory currentFiles |> ignore

        try
            currentFiles |> List.iter File.Delete
            IO.enumerateFiles stagedDir |> moveFiles projectDirectory stagedDir
        with _ ->
            (IO.createSnapshot outputs projectDirectory).TimestampedFiles.Keys
            |> Seq.iter File.Delete
            if Directory.Exists backupDir then
                IO.enumerateFiles backupDir |> moveFiles projectDirectory backupDir
            reraise()
    finally
        if Directory.Exists transactionDir then IO.deleteAny transactionDir

let clearCache () =
    IO.deleteAny (createCache())

let clearHomeCache () =
    IO.deleteAny (createHome())

let clearTemp () =
    IO.deleteAny (createTmp())

let createDirectories() =
    createTerrabuildProfile() |> ignore
    createCache() |> ignore
    createHome() |> ignore
    createTmp() |> ignore

let pruneCacheEntries cacheDir cutoff =
    let emptySummary: PruneSummary = {
        Scanned = 0
        Pruned = 0
        Skipped = 0
    }

    if Directory.Exists cacheDir |> not then
        emptySummary
    else
        let pruneEntry (summary: PruneSummary) entryDir =
            let originFile = FS.combinePath entryDir originFilename
            let summary =
                { summary with
                    Scanned = summary.Scanned + 1 }

            if File.Exists originFile |> not then
                { summary with Skipped = summary.Skipped + 1 }
            else
                let lastAccessedAt = File.GetLastWriteTimeUtc(originFile)
                if lastAccessedAt > cutoff then
                    { summary with Skipped = summary.Skipped + 1 }
                else
                    try
                        withEntryLock entryDir (fun () ->
                            if File.Exists originFile && File.GetLastWriteTimeUtc(originFile) <= cutoff then
                                Directory.Delete(entryDir, true)
                                true
                            else
                                false)
                        |> function
                            | true -> { summary with Pruned = summary.Pruned + 1 }
                            | false -> { summary with Skipped = summary.Skipped + 1 }
                    with exn ->
                        Log.Warning(exn, "Failed to prune cache entry {EntryDir}", entryDir)
                        { summary with Skipped = summary.Skipped + 1 }

        IO.enumerateDirs cacheDir
        |> Seq.collect IO.enumerateDirs
        |> Seq.collect IO.enumerateDirs
        |> Seq.fold pruneEntry emptySummary

let pruneCache days =
    let cutoff = DateTime.UtcNow - TimeSpan.FromDays(days |> float)
    pruneCacheEntries (createCache()) cutoff


type NewEntry(entryDir: string, useRemote: bool, id: string, storage: Contracts.IStorage, masterKey: byte[] option) =
    let stagingDir = createStagingDirectory entryDir
    let logsDir = FS.combinePath stagingDir "logs"
    let outputsDir = FS.combinePath stagingDir "outputs"
    let mutable logNum = 1
    let mutable completed = false

    let hasMaterializedOutputs () =
        Directory.Exists outputsDir &&
        (IO.enumerateFiles outputsDir |> List.isEmpty |> not)

    do
        IO.createDirectory logsDir
        // NOTE: outputs is created on demand only

    let write (summary: TargetSummary) file =
        let stored =
            { StoredTargetSummary.Project = summary.Project
              StoredTargetSummary.Target = summary.Target
              StoredTargetSummary.Operations =
                summary.Operations
                |> List.map (fun stepGroup ->
                    stepGroup
                    |> List.map (fun step -> { step with Log = IO.getFilename step.Log }))
              StoredTargetSummary.Outputs =
                if summary.HasOutputs && hasMaterializedOutputs () then Some "outputs"
                else None
              StoredTargetSummary.IsSuccessful = summary.IsSuccessful
              StoredTargetSummary.StartedAt = summary.StartedAt
              StoredTargetSummary.EndedAt = summary.EndedAt
              StoredTargetSummary.Duration = summary.Duration
              StoredTargetSummary.Cache = summary.Cache }

        stored |> Json.Serialize |> IO.writeTextFile file

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

        member _.StoreOutputs sourceDir entries =
            IO.copyFiles outputsDir sourceDir entries

        member _.StoreLogs entries =
            for entry in entries do
                File.Copy(entry, FS.combinePath logsDir (IO.getFilename entry), true)

        member _.Complete summary =
            if completed then raiseBugError $"Cache entry '{id}' has already been completed"
            completed <- true
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
                        name
                    finally
                        IO.deleteAny encryptedFile
                        IO.deleteAny compressFile
                        IO.deleteAny tarFile

                let genFinalSummary() =
                    FS.combinePath logsDir "summary.json" |> write summary

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

            stagingDir |> setOrigin Origin.Local
            publishDirectory entryDir stagingDir
            files


type Cache(storage: Contracts.IStorage, masterKey: byte[] option) =
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

    let tryLoadSummary entryDir =
        let logsDir = FS.combinePath entryDir "logs"
        let summaryFile = FS.combinePath logsDir summaryFilename
        try
            let stored = summaryFile |> IO.readTextFile |> Json.Deserialize<StoredTargetSummary>
            let summary =
                { TargetSummary.Project = stored.Project
                  TargetSummary.Target = stored.Target
                  TargetSummary.Operations =
                    stored.Operations
                    |> List.map (fun stepGroup ->
                        stepGroup
                        |> List.map (fun stepLog -> { stepLog with Log = FS.combinePath logsDir stepLog.Log }))
                  TargetSummary.HasOutputs = stored.Outputs.IsSome
                  TargetSummary.IsSuccessful = stored.IsSuccessful
                  TargetSummary.StartedAt = stored.StartedAt
                  TargetSummary.EndedAt = stored.EndedAt
                  TargetSummary.Duration = stored.Duration
                  TargetSummary.Cache = stored.Cache }
            Some summary
        with
            | exn ->
                Log.Error(exn, "Failed to process content {summaryFile}", summaryFile)
                None

    let tryLoadCompleteEntry entryDir =
        let originFile = FS.combinePath entryDir originFilename
        if File.Exists originFile then
            tryLoadSummary entryDir |> Option.map (fun summary -> getOrigin entryDir, summary)
        else
            None

    let downloadEntry id includeOutputs entryDir =
        let stagingDir = createStagingDirectory entryDir
        let stagingLogs = FS.combinePath stagingDir "logs"
        let stagingOutputs = FS.combinePath stagingDir "outputs"
        try
            if tryDownload stagingLogs id "logs" |> not then
                None
            else
                match tryLoadSummary stagingDir with
                | None -> None
                | Some summary ->
                    let outputsReady =
                        match includeOutputs, summary.HasOutputs with
                        | true, true -> tryDownload stagingOutputs id "outputs"
                        | _ -> true

                    if outputsReady then
                        stagingDir |> setOrigin Origin.Remote
                        replaceDirectory entryDir stagingDir
                        tryLoadSummary entryDir |> Option.map (fun loaded -> Origin.Remote, loaded)
                    else
                        None
        finally
            if Directory.Exists stagingDir then IO.deleteAny stagingDir

    let getSummaryOnly useRemote id =
        let entryDir = FS.combinePath (createCache()) id
        match cachedSummaries.TryGetValue(id) with
        | true, (Some _ as originSummary) ->
            touchOrigin entryDir
            originSummary
        | true, originSummary -> originSummary
        | false, _ ->
            let originSummary =
                withEntryLock entryDir (fun () ->
                    match tryLoadCompleteEntry entryDir with
                    | Some originSummary ->
                        touchOrigin entryDir
                        Some originSummary
                    | None when useRemote -> downloadEntry id false entryDir
                    | None -> None)
            cachedSummaries.TryAdd(id, originSummary) |> ignore
            originSummary

    interface ICache with
        member _.TryGetSummaryOnly useRemote id = getSummaryOnly useRemote id

        member _.CanRestore useRemote id summary =
            if not summary.HasOutputs || useRemote then
                true
            else
                let entryDir = FS.combinePath (createCache()) id
                Directory.Exists(FS.combinePath entryDir "outputs")

        member _.TryGetSummary useRemote id =
            getSummaryOnly useRemote id |> Option.map snd

        member _.Restore useRemote id outputs projectDirectory =
            let entryDir = FS.combinePath (createCache()) id
            let originSummary =
                withEntryLock entryDir (fun () ->
                    let available =
                        match tryLoadCompleteEntry entryDir with
                        | Some (origin, summary) when not summary.HasOutputs || Directory.Exists(FS.combinePath entryDir "outputs") ->
                            Some (origin, summary)
                        | _ when useRemote -> downloadEntry id true entryDir
                        | _ -> None

                    match available with
                    | Some (_, summary) when summary.HasOutputs ->
                        restoreOutputsTransaction entryDir outputs projectDirectory
                    | _ -> ()
                    available)

            originSummary |> Option.map snd

        member _.GetEntry useRemote id : IEntry =
            cachedSummaries.TryRemove(id) |> ignore
            let entryDir = FS.combinePath (createCache()) id
            NewEntry(entryDir, useRemote, id, storage, masterKey)
