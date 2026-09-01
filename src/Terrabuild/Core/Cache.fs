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
type OutputState =
    | NotManaged
    | Empty
    | Stored

[<RequireQualifiedAccess>]
type TargetSummary = {
    Project: string
    Target: string
    Operations: OperationSummary list list
    Outputs: OutputState
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
    Outputs: OutputState
    IsSuccessful: bool
    StartedAt: DateTime
    EndedAt: DateTime
    Duration: TimeSpan
    Cache: GraphDef.ArtifactMode
}

[<RequireQualifiedAccess>]
type private RemoteBlob = {
    Path: string
    Sha256: string
}

[<RequireQualifiedAccess>]
type private RemoteManifest = {
    Version: int
    Generation: string
    Logs: RemoteBlob
    Outputs: RemoteBlob option
}

[<RequireQualifiedAccess>]
type private RemoteManifestResult =
    | Missing
    | Invalid
    | Found of RemoteManifest


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
    inherit IDisposable
    abstract NextLogFile: unit -> string
    abstract StoreOutputs: sourceDir:string -> entries:string list -> OutputState
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
let private stagingLeaseFilename = ".lease"
let private remoteManifestFilename = "remote.json"

type private ProfileUsageLease(stream: FileStream, path: string) =
    interface IDisposable with
        member _.Dispose() =
            stream.Dispose()
            try File.Delete(path)
            with
            | :? FileNotFoundException
            | :? DirectoryNotFoundException -> ()

let createTerrabuildProfile() =
    let tbDir = FS.combinePath ("HOME" |> Environment.envVar |> Option.get) ".terrabuild"
    IO.createDirectory tbDir
    tbDir

let private acquireProfileGate profile =
    let path = FS.combinePath profile "locks/profile.lock"
    path |> FS.parentDirectory |> Option.iter IO.createDirectory

    let rec acquire () =
        try new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)
        with :? IOException ->
            Thread.Sleep(25)
            acquire ()

    acquire ()

let internal acquireProfileUsage () =
    let profile = createTerrabuildProfile()
    use _gate = acquireProfileGate profile
    let path = FS.combinePath profile $"locks/runs/{Guid.NewGuid():N}.lease"
    path |> FS.parentDirectory |> Option.iter IO.createDirectory
    let stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None)
    new ProfileUsageLease(stream, path) :> IDisposable

let internal acquireProfileClearLease () =
    let profile = createTerrabuildProfile()
    let gate = acquireProfileGate profile
    try
        let leasesDir = FS.combinePath profile "locks/runs"
        let active = ResizeArray<string>()
        if Directory.Exists leasesDir then
            for path in Directory.EnumerateFiles(leasesDir, "*.lease") do
                try
                    use stale = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
                    stale.Dispose()
                    File.Delete(path)
                with :? IOException ->
                    active.Add(path)

        if active.Count > 0 then
            raiseInvalidArg $"Cannot clear Terrabuild data while {active.Count} other Terrabuild process(es) are active."
        gate :> IDisposable
    with _ ->
        gate.Dispose()
        reraise()

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
    try
        if File.Exists originFile then
            File.SetLastWriteTimeUtc(originFile, DateTime.UtcNow)
    with
    | :? FileNotFoundException
    | :? DirectoryNotFoundException -> ()

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

let private acquireStagingLease stagingDir =
    let leaseFile = FS.combinePath stagingDir stagingLeaseFilename
    new FileStream(leaseFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)

let private isStagingDirectory entryDir =
    let name = IO.getFilename entryDir
    name.StartsWith(".", StringComparison.Ordinal)
    && name.Contains(".tmp-", StringComparison.Ordinal)

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

let private recoverReplacedDirectory entryDir =
    let parent =
        entryDir
        |> FS.parentDirectory
        |> Option.defaultWith (fun () -> raiseBugError $"Cache entry '{entryDir}' has no parent directory")

    if Directory.Exists parent then
        let backups =
            Directory.EnumerateDirectories(parent, $"{IO.getFilename entryDir}.old-*")
            |> Seq.sortByDescending Directory.GetLastWriteTimeUtc
            |> List.ofSeq

        match Directory.Exists entryDir, backups with
        | false, newest :: older ->
            Directory.Move(newest, entryDir)
            older |> List.iter IO.deleteAny
        | true, backups ->
            backups |> List.iter IO.deleteAny
        | false, [] -> ()

let private publishDirectory entryDir stagingDir =
    withEntryLock entryDir (fun () ->
        recoverReplacedDirectory entryDir
        replaceDirectory entryDir stagingDir)

type private RestoreTransaction = {
    ProjectDirectory: string
    Outputs: string list
}

[<RequireQualifiedAccess>]
type private RestoreTransactionIndex = {
    TransactionDirectory: string
    ProjectDirectory: string
}

let private restoreMetadataFilename = "transaction.json"
let private restoreStateFilename = "state"
let private restorePrepared = "prepared"
let private restoreApplying = "applying"
let private restoreCommitted = "committed"

let private restoreIndexDirectory () = FS.combinePath (createTerrabuildProfile()) "transactions/restores"

let private restoreIndexPath transactionDir =
    let id = (Hash.sha256 transactionDir).ToLowerInvariant()
    FS.combinePath (restoreIndexDirectory()) $"{id}.json"

let private moveFiles targetDir baseDir files =
    for file in files do
        let relative = FS.relativePath baseDir file
        let target = FS.combinePath targetDir relative
        target
        |> FS.parentDirectory
        |> Option.iter IO.createDirectory
        File.Move(file, target, true)

let private writeRestoreFile transactionDir filename contents =
    let destination = FS.combinePath transactionDir filename
    let temporary = $"{destination}.{Guid.NewGuid():N}.tmp"
    IO.writeTextFile temporary contents
    File.Move(temporary, destination, true)

let private registerRestoreTransaction transactionDir projectDirectory =
    let directory = restoreIndexDirectory()
    IO.createDirectory directory
    let index : RestoreTransactionIndex =
        { RestoreTransactionIndex.TransactionDirectory = transactionDir
          RestoreTransactionIndex.ProjectDirectory = projectDirectory }
    index |> Json.Serialize |> writeRestoreFile directory (IO.getFilename (restoreIndexPath transactionDir))
    restoreIndexPath transactionDir

let private writeRestoreState transactionDir state =
    writeRestoreFile transactionDir restoreStateFilename state

let private rollbackRestore transactionDir (transaction: RestoreTransaction) =
    let backupDir = FS.combinePath transactionDir "backup"
    (IO.createSnapshot (Set transaction.Outputs) transaction.ProjectDirectory).TimestampedFiles.Keys
    |> Seq.iter File.Delete
    if Directory.Exists backupDir then
        IO.copyFiles transaction.ProjectDirectory backupDir (IO.enumerateFiles backupDir) |> ignore

let private restoreTransactionPrefix projectDirectory =
    let projectHash = (Hash.sha256 projectDirectory).Substring(0, 12).ToLowerInvariant()
    $".terrabuild-restore-{projectHash}-"

let private recoverOutputTransactionsUnlocked projectDirectory parent =
    let pattern = $"{restoreTransactionPrefix projectDirectory}*"
    for transactionDir in Directory.EnumerateDirectories(parent, pattern) do
        let metadataFile = FS.combinePath transactionDir restoreMetadataFilename
        if File.Exists metadataFile then
            let transaction = metadataFile |> IO.readTextFile |> Json.Deserialize<RestoreTransaction>
            if Path.GetFullPath(transaction.ProjectDirectory) = projectDirectory then
                let stateFile = FS.combinePath transactionDir restoreStateFilename
                let state =
                    if File.Exists stateFile then IO.readTextFile stateFile
                    else restorePrepared
                match state with
                | state when state = restoreApplying -> rollbackRestore transactionDir transaction
                | state when state = restorePrepared || state = restoreCommitted -> ()
                | state -> raiseBugError $"Unknown cache restore transaction state '{state}' in '{transactionDir}'"
                IO.deleteAny transactionDir
        else
            IO.deleteAny transactionDir

let private restoreLockEntry projectDirectory =
    let profile = createTerrabuildProfile()
    let projectHash = (Hash.sha256 projectDirectory).ToLowerInvariant()
    FS.combinePath profile $"locks/restores/{projectHash}"

let internal recoverOutputTransactions projectDirectory =
    let projectDirectory = Path.GetFullPath(projectDirectory)
    let parent =
        projectDirectory
        |> FS.parentDirectory
        |> Option.defaultWith (fun () -> raiseBugError $"Project directory '{projectDirectory}' has no parent")
    withEntryLock (restoreLockEntry projectDirectory) (fun () ->
        recoverOutputTransactionsUnlocked projectDirectory parent)

let private isWithinDirectory root path =
    let relative = Path.GetRelativePath(root, path)
    relative = "."
    || (Path.IsPathRooted(relative) |> not
        && relative <> ".."
        && relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) |> not)

let internal recoverWorkspaceOutputTransactions workspaceDirectory =
    let workspaceDirectory = Path.GetFullPath(workspaceDirectory)

    // A transaction for the workspace root is its sibling, not its child.
    recoverOutputTransactions workspaceDirectory

    let indexDirectory = restoreIndexDirectory()
    if Directory.Exists indexDirectory then
        for indexFile in Directory.EnumerateFiles(indexDirectory, "*.json") do
            try
                let index = indexFile |> IO.readTextFile |> Json.Deserialize<RestoreTransactionIndex>
                let projectDirectory = Path.GetFullPath(index.ProjectDirectory)
                if isWithinDirectory workspaceDirectory projectDirectory then
                    recoverOutputTransactions projectDirectory
                    IO.deleteAny indexFile
            with exn ->
                Log.Warning(exn, "Discarding unreadable restore transaction index {IndexFile}", indexFile)
                IO.deleteAny indexFile

    let legacyMarker =
        let workspaceHash = (Hash.sha256 workspaceDirectory).ToLowerInvariant()
        FS.combinePath (createTerrabuildProfile()) $"transactions/legacy-scans/{workspaceHash}.done"

    if Directory.Exists workspaceDirectory && not (File.Exists legacyMarker) then
        let projectDirectories =
            Directory.EnumerateDirectories(workspaceDirectory, ".terrabuild-restore-*", SearchOption.AllDirectories)
            |> Seq.choose (fun transactionDir ->
                let metadataFile = FS.combinePath transactionDir restoreMetadataFilename
                if File.Exists metadataFile then
                    let transaction = metadataFile |> IO.readTextFile |> Json.Deserialize<RestoreTransaction>
                    let projectDirectory = Path.GetFullPath(transaction.ProjectDirectory)
                    if isWithinDirectory workspaceDirectory projectDirectory then Some projectDirectory
                    else None
                else
                    None)
            |> Set.ofSeq

        projectDirectories |> Set.iter recoverOutputTransactions
        legacyMarker |> FS.parentDirectory |> Option.iter IO.createDirectory
        writeRestoreFile (FS.parentDirectory legacyMarker |> Option.get) (IO.getFilename legacyMarker) "complete"

let private restoreOutputsTransaction cachedOutputs outputs projectDirectory =
    let projectDirectory = Path.GetFullPath(projectDirectory)
    let parent =
        projectDirectory
        |> FS.parentDirectory
        |> Option.defaultWith (fun () -> raiseBugError $"Project directory '{projectDirectory}' has no parent")

    withEntryLock (restoreLockEntry projectDirectory) (fun () ->
        recoverOutputTransactionsUnlocked projectDirectory parent

        let transactionDir = FS.combinePath parent $"{restoreTransactionPrefix projectDirectory}{Guid.NewGuid():N}"
        let stagedDir = FS.combinePath transactionDir "staged"
        let backupDir = FS.combinePath transactionDir "backup"
        let cachedFiles =
            cachedOutputs
            |> Option.map IO.enumerateFiles
            |> Option.defaultValue []
        let currentFiles = (IO.createSnapshot outputs projectDirectory).TimestampedFiles.Keys |> List.ofSeq
        let transaction = {
            ProjectDirectory = projectDirectory
            Outputs = outputs |> Set.toList
        }
        let mutable cleanup = false
        let mutable indexFile: string option = None

        try
            IO.createDirectory stagedDir
            IO.createDirectory backupDir
            transaction
            |> Json.Serialize
            |> writeRestoreFile transactionDir restoreMetadataFilename
            cachedOutputs
            |> Option.iter (fun cachedOutputs -> IO.copyFiles stagedDir cachedOutputs cachedFiles |> ignore)
            IO.copyFiles backupDir projectDirectory currentFiles |> ignore
            writeRestoreState transactionDir restorePrepared
            // From this point forward the transaction may mutate workspace files and must be globally discoverable.
            indexFile <- Some (registerRestoreTransaction transactionDir projectDirectory)

            try
                writeRestoreState transactionDir restoreApplying
                currentFiles |> List.iter File.Delete
                IO.enumerateFiles stagedDir |> moveFiles projectDirectory stagedDir
                writeRestoreState transactionDir restoreCommitted
                cleanup <- true
            with _ ->
                rollbackRestore transactionDir transaction
                cleanup <- true
                reraise()
        finally
            if cleanup && Directory.Exists transactionDir then IO.deleteAny transactionDir
            if cleanup then indexFile |> Option.iter IO.deleteAny)

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
                let staleStaging =
                    isStagingDirectory entryDir
                    && Directory.GetLastWriteTimeUtc(entryDir) <= cutoff

                if not staleStaging then
                    { summary with Skipped = summary.Skipped + 1 }
                else
                    try
                        let leaseAvailable =
                            try
                                use _lease = acquireStagingLease entryDir
                                true
                            with :? IOException ->
                                false

                        if leaseAvailable && Directory.Exists entryDir then
                            Directory.Delete(entryDir, true)
                            { summary with Pruned = summary.Pruned + 1 }
                        else
                            { summary with Skipped = summary.Skipped + 1 }
                    with exn ->
                        Log.Warning(exn, "Failed to prune cache staging directory {EntryDir}", entryDir)
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
    let stagingLease =
        try acquireStagingLease stagingDir
        with _ ->
            IO.deleteAny stagingDir
            reraise()
    let logsDir = FS.combinePath stagingDir "logs"
    let outputsDir = FS.combinePath stagingDir "outputs"
    let mutable logNum = 1
    let mutable completed = false
    let mutable leaseReleased = false

    let releaseStagingLease () =
        if not leaseReleased then
            leaseReleased <- true
            stagingLease.Dispose()
            IO.deleteAny (FS.combinePath stagingDir stagingLeaseFilename)

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
                match summary.Outputs with
                | OutputState.Stored when hasMaterializedOutputs () -> OutputState.Stored
                | OutputState.Stored -> OutputState.Empty
                | state -> state
              StoredTargetSummary.IsSuccessful = summary.IsSuccessful
              StoredTargetSummary.StartedAt = summary.StartedAt
              StoredTargetSummary.EndedAt = summary.EndedAt
              StoredTargetSummary.Duration = summary.Duration
              StoredTargetSummary.Cache = summary.Cache }

        stored |> Json.Serialize |> IO.writeTextFile file

    let uploadBlob (sourceDir: string) (path: string) : RemoteBlob =
        let mutable tarFile: string | null = null
        let mutable compressFile: string | null = null
        let mutable encryptedFile: string | null = null
        try
            tarFile <- Compression.tar sourceDir
            compressFile <- Compression.compress (tarFile |> nonNull)
            encryptedFile <- Encryption.encrypt masterKey id (compressFile |> nonNull)
            let encryptedFile = encryptedFile |> nonNull
            storage.Upload path encryptedFile
            { RemoteBlob.Path = path
              RemoteBlob.Sha256 = Hash.sha256file encryptedFile }
        finally
            IO.deleteAny encryptedFile
            IO.deleteAny compressFile
            IO.deleteAny tarFile

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
            match IO.copyFiles outputsDir sourceDir entries with
            | Some _ -> OutputState.Stored
            | None -> OutputState.Empty

        member _.StoreLogs entries =
            for entry in entries do
                File.Copy(entry, FS.combinePath logsDir (IO.getFilename entry), true)

        member _.Complete summary =
            if completed then raiseBugError $"Cache entry '{id}' has already been completed"
            completed <- true
            let files =
                let genFinalSummary() =
                    FS.combinePath logsDir "summary.json" |> write summary

                if useRemote then
                    let generation = Guid.NewGuid().ToString("N")
                    let generationRoot = $"{id}/generations/{generation}"
                    let outputs =
                        if Directory.Exists outputsDir then
                            Some (uploadBlob outputsDir $"{generationRoot}/outputs")
                        else
                            None
                    genFinalSummary()
                    let logs = uploadBlob logsDir $"{generationRoot}/logs"
                    let manifest : RemoteManifest =
                        { RemoteManifest.Version = 1
                          RemoteManifest.Generation = generation
                          RemoteManifest.Logs = logs
                          RemoteManifest.Outputs = outputs }
                    let manifestFile = FS.combinePath stagingDir remoteManifestFilename
                    manifest |> Json.Serialize |> IO.writeTextFile manifestFile
                    storage.Upload $"{id}/manifest" manifestFile
                    [ if outputs.IsSome then yield "outputs"
                      yield "logs" ]
                else
                    genFinalSummary()
                    []

            stagingDir |> setOrigin Origin.Local
            releaseStagingLease ()
            publishDirectory entryDir stagingDir
            files

        member _.Dispose() =
            releaseStagingLease ()
            if Directory.Exists stagingDir then IO.deleteAny stagingDir


type Cache(storage: Contracts.IStorage, masterKey: byte[] option) =
    let cachedSummaries = System.Collections.Concurrent.ConcurrentDictionary<string, (Origin*TargetSummary) option>()

    let tryDownloadBlob (targetDir: string) (id: string) (blob: RemoteBlob) =
        match storage.TryDownload blob.Path with
        | Some file ->
            let mutable decryptedFile: string option = None
            let mutable decompressedFile: string | null = null
            try
                try
                    if not (String.Equals(Hash.sha256file file, blob.Sha256, StringComparison.OrdinalIgnoreCase)) then
                        Log.Warning("Ignoring remote cache blob {BlobPath} because its digest does not match", blob.Path)
                        false
                    else
                        decryptedFile <- Encryption.tryDecrypt masterKey id file
                        match decryptedFile with
                        | Some decryptedFile ->
                            decompressedFile <- Compression.uncompress decryptedFile
                            Compression.untar targetDir (decompressedFile |> nonNull)
                            true
                        | _ -> false
                with exn ->
                    Log.Warning(exn, "Ignoring unreadable remote cache blob {BlobPath}", blob.Path)
                    false
            finally
                IO.deleteAny decompressedFile
                IO.deleteAny file
        | _ ->
            false

    let tryDownloadLegacy (targetDir: string) (id: string) (name: string) =
        match storage.TryDownload $"{id}/{name}" with
        | Some file ->
            let mutable decryptedFile: string option = None
            let mutable decompressedFile: string | null = null
            try
                try
                    decryptedFile <- Encryption.tryDecrypt masterKey id file
                    match decryptedFile with
                    | Some decryptedFile ->
                        decompressedFile <- Compression.uncompress decryptedFile
                        Compression.untar targetDir (decompressedFile |> nonNull)
                        true
                    | _ -> false
                with exn ->
                    Log.Warning(exn, "Ignoring unreadable legacy remote cache blob {BlobPath}", $"{id}/{name}")
                    false
            finally
                IO.deleteAny decompressedFile
                IO.deleteAny file
        | _ -> false

    let tryReadRemoteManifest (id: string) =
        match storage.TryDownload $"{id}/manifest" with
        | None -> RemoteManifestResult.Missing
        | Some file ->
            try
                try
                    let manifest = file |> IO.readTextFile |> Json.Deserialize<RemoteManifest>
                    if manifest.Version = 1 && not (String.IsNullOrWhiteSpace manifest.Generation) then
                        RemoteManifestResult.Found manifest
                    else
                        Log.Warning("Ignoring unsupported remote cache manifest for {CacheEntryId}", id)
                        RemoteManifestResult.Invalid
                with exn ->
                    Log.Warning(exn, "Ignoring unreadable remote cache manifest for {CacheEntryId}", id)
                    RemoteManifestResult.Invalid
            finally
                IO.deleteAny file

    let tryLoadLocalRemoteManifest (entryDir: string) =
        let file = FS.combinePath entryDir remoteManifestFilename
        if File.Exists file then
            try file |> IO.readTextFile |> Json.Deserialize<RemoteManifest> |> Some
            with exn ->
                Log.Warning(exn, "Ignoring unreadable local remote cache manifest {ManifestFile}", file)
                None
        else None

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
                  TargetSummary.Outputs = stored.Outputs
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

    let downloadEntry (id: string) includeOutputs entryDir =
        let stagingDir = createStagingDirectory entryDir
        let stagingLease =
            try acquireStagingLease stagingDir
            with _ ->
                IO.deleteAny stagingDir
                reraise()
        let mutable leaseReleased = false
        let releaseStagingLease () =
            if not leaseReleased then
                leaseReleased <- true
                stagingLease.Dispose()
                IO.deleteAny (FS.combinePath stagingDir stagingLeaseFilename)
        let stagingLogs = FS.combinePath stagingDir "logs"
        let stagingOutputs = FS.combinePath stagingDir "outputs"
        try
            let manifestResult = tryReadRemoteManifest id
            let downloadedLogs, manifest =
                match manifestResult with
                | RemoteManifestResult.Found manifest ->
                    tryDownloadBlob stagingLogs id manifest.Logs, Some manifest
                | RemoteManifestResult.Missing ->
                    tryDownloadLegacy stagingLogs id "logs", None
                | RemoteManifestResult.Invalid -> false, None

            if not downloadedLogs then
                None
            else
                match tryLoadSummary stagingDir with
                | None -> None
                | Some summary ->
                    let outputsReady =
                        match includeOutputs, summary.Outputs, manifest with
                        | true, OutputState.Stored, Some manifest ->
                            manifest.Outputs
                            |> Option.map (tryDownloadBlob stagingOutputs id)
                            |> Option.defaultValue false
                        | true, OutputState.Stored, None -> tryDownloadLegacy stagingOutputs id "outputs"
                        | _ -> true

                    if outputsReady then
                        manifest
                        |> Option.iter (fun manifest ->
                            manifest
                            |> Json.Serialize
                            |> IO.writeTextFile (FS.combinePath stagingDir remoteManifestFilename))
                        stagingDir |> setOrigin Origin.Remote
                        releaseStagingLease ()
                        replaceDirectory entryDir stagingDir
                        tryLoadSummary entryDir |> Option.map (fun loaded -> Origin.Remote, loaded)
                    else
                        None
        finally
            releaseStagingLease ()
            if Directory.Exists stagingDir then IO.deleteAny stagingDir

    let getSummaryOnly useRemote (id: string) =
        let entryDir = FS.combinePath (createCache()) id
        match cachedSummaries.TryGetValue(id) with
        | true, (Some _ as originSummary) ->
            touchOrigin entryDir
            originSummary
        | true, originSummary -> originSummary
        | false, _ ->
            let originSummary =
                withEntryLock entryDir (fun () ->
                    recoverReplacedDirectory entryDir
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
            match summary.Outputs with
            | OutputState.NotManaged
            | OutputState.Empty -> true
            | OutputState.Stored ->
                let entryDir = FS.combinePath (createCache()) id
                if Directory.Exists(FS.combinePath entryDir "outputs") then true
                elif useRemote then
                    match tryLoadLocalRemoteManifest entryDir with
                    | Some manifest ->
                        manifest.Outputs
                        |> Option.map (fun blob -> storage.Exists blob.Path)
                        |> Option.defaultValue false
                    | None ->
                        match tryReadRemoteManifest id with
                        | RemoteManifestResult.Found manifest ->
                            manifest.Outputs
                            |> Option.map (fun blob -> storage.Exists blob.Path)
                            |> Option.defaultValue false
                        | RemoteManifestResult.Missing -> storage.Exists $"{id}/outputs"
                        | RemoteManifestResult.Invalid -> false
                else false

        member _.TryGetSummary useRemote id =
            getSummaryOnly useRemote id |> Option.map snd

        member _.Restore useRemote id outputs projectDirectory =
            let entryDir = FS.combinePath (createCache()) id
            let originSummary =
                withEntryLock entryDir (fun () ->
                    recoverReplacedDirectory entryDir
                    let available =
                        match tryLoadCompleteEntry entryDir with
                        | Some (origin, summary) when summary.Outputs <> OutputState.Stored || Directory.Exists(FS.combinePath entryDir "outputs") ->
                            Some (origin, summary)
                        | _ when useRemote -> downloadEntry id true entryDir
                        | _ -> None

                    match available with
                    | Some (_, summary) when summary.Outputs = OutputState.Stored ->
                        restoreOutputsTransaction (Some (FS.combinePath entryDir "outputs")) outputs projectDirectory
                    | Some (_, summary) when summary.Outputs = OutputState.Empty ->
                        restoreOutputsTransaction None outputs projectDirectory
                    | _ -> ()
                    available)

            originSummary |> Option.map snd

        member _.GetEntry useRemote id : IEntry =
            cachedSummaries.TryRemove(id) |> ignore
            let entryDir = FS.combinePath (createCache()) id
            new NewEntry(entryDir, useRemote, id, storage, masterKey)
