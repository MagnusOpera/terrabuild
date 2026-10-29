module internal WorkspaceSnapshotCache

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Threading

// Short expiry also refreshes inputs outside the workspace, such as environment
// variables and imported scripts. File events invalidate workspace inputs sooner.
type Cache<'Key, 'Value when 'Key: equality and 'Key: not null>(workspace: string, lifetime: TimeSpan) =
    let gate = obj()
    let entries = Dictionary<'Key, 'Value * int64 * int64>()
    let mutable generation = 0L
    let invalidate () = Interlocked.Increment(&generation) |> ignore
    let watcher = new FileSystemWatcher(workspace, IncludeSubdirectories = true)
    do
        watcher.Changed.Add(fun _ -> invalidate())
        watcher.Created.Add(fun _ -> invalidate())
        watcher.Deleted.Add(fun _ -> invalidate())
        watcher.Renamed.Add(fun _ -> invalidate())
        watcher.Error.Add(fun _ -> invalidate())
        try watcher.EnableRaisingEvents <- true
        with :? IOException as error ->
            // Watch limits must not prevent the console from starting.
            Serilog.Log.Warning(error, "Workspace watching unavailable; console configuration expires after {Lifetime}", lifetime)

    member _.Invalidate() = invalidate()

    member _.Get(key: 'Key, load: unit -> 'Value) =
        lock gate (fun () ->
            let version = Volatile.Read(&generation)
            match entries.TryGetValue(key) with
            | true, (value, storedVersion, timestamp)
                when storedVersion = version && Stopwatch.GetElapsedTime(timestamp) < lifetime -> value
            | _ ->
                let value = load()
                // Keep the original generation so changes during loading force a reload.
                if entries.Count >= 16 then entries.Clear()
                entries[key] <- value, version, Stopwatch.GetTimestamp()
                value)

    interface IDisposable with
        member _.Dispose() = watcher.Dispose()
