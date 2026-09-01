module ExclusiveFileLock

open System
open System.Diagnostics
open System.IO
open System.Threading
open Serilog

let private errorCode (exn: IOException) = exn.HResult &&& 0xffff

let internal isContentionError (exn: IOException) =
    // Unix EAGAIN/EACCES values used by .NET file-share locks, macOS EAGAIN,
    // and Windows sharing/lock violations. Other I/O failures must surface.
    match errorCode exn with
    | 11 | 13 | 32 | 33 | 35 -> true
    | _ -> false

let acquire description path =
    let timer = Stopwatch.StartNew()
    let mutable waiting = false
    let mutable nextWarning = TimeSpan.FromSeconds(30.)
    let mutable stream: FileStream option = None

    while stream.IsNone do
        try
            stream <- Some(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        with
        | :? IOException as exn when isContentionError exn ->
            if not waiting then
                Log.Debug("Waiting for {LockDescription} at {LockPath}", description, path)
                waiting <- true
            elif timer.Elapsed >= nextWarning then
                Log.Warning("Still waiting for {LockDescription} at {LockPath} after {WaitDuration}", description, path, timer.Elapsed)
                nextWarning <- nextWarning.Add(TimeSpan.FromMinutes(1.))
            Thread.Sleep(25)

    stream.Value
