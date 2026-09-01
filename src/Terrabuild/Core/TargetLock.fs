module TargetLock

open System
open System.IO

type private Lease(streams: FileStream list) =
    interface IDisposable with
        member _.Dispose() =
            streams |> List.iter _.Dispose()

let internal lockFilePath profileDir name =
    let slug =
        name
        |> String.slugify
        |> String.cut 48
        |> function
            | "" -> "lock"
            | value -> value
    let hash = (Hash.sha256 name).Substring(0, 12).ToLowerInvariant()
    FS.combinePath profileDir $"locks/targets/{slug}-{hash}.lock"

let private acquireFile (name: string) (path: string) =
    path
    |> FS.parentDirectory
    |> Option.iter IO.createDirectory

    ExclusiveFileLock.acquire $"target lock '{name}'" path

let internal acquireAt profileDir (names: Set<string>) =
    let mutable leases: FileStream list = []
    try
        for name in names |> Seq.sort do
            let path = lockFilePath profileDir name
            leases <- acquireFile name path :: leases
        new Lease(leases) :> IDisposable
    with _ ->
        leases |> List.iter _.Dispose()
        reraise()

let acquire names =
    acquireAt (Cache.createTerrabuildProfile()) names

let internal clearAt profileDir =
    let lockDir = FS.combinePath profileDir "locks"
    if Directory.Exists(lockDir) then
        for path in Directory.EnumerateFiles(lockDir, "*.lock", SearchOption.AllDirectories) do
            try
                use _lease = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Delete)
                File.Delete(path)
            with :? IOException ->
                ()

let clear () =
    clearAt (Cache.createTerrabuildProfile())
