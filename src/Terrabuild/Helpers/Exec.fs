module Exec
open System.Diagnostics
open System
open System.IO
open Errors
open Serilog
open Environment
open System.Runtime.InteropServices
open System.Collections.Concurrent


// ----------------------
// Track all children
// ----------------------
let private children = ConcurrentBag<Process>()




let private createProcess workingDir command args redirect =
    let psi = ProcessStartInfo (FileName = command,
                                Arguments = args,
                                UseShellExecute = false,
                                WorkingDirectory = workingDir,
                                RedirectStandardOutput = redirect,
                                RedirectStandardError = redirect)
    let proc = new Process(StartInfo = psi)

    if not (proc.Start()) then
        failwithf "Failed to start process: %s" command

    children.Add proc

    if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
        Native.Windows.assign proc
    elif RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
         RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
        // Put child in its own process group
        Native.Posix.setpgid(proc.Id, 0) |> ignore

    proc



// ----------------------
// Cleanup hooks
// ----------------------
let cleanup () =
    // As a fallback, ensure tracked children are killed
    for proc in children do
        try
            if not proc.HasExited then
                proc.Kill(true)   // Kill entire tree
        with _ -> ()



type CaptureResult =
    | Success of string*int
    | Error of string*int

let execCaptureOutput (workingDir: string) (command: string) (args: string) =
    Log.Debug($"Running and capturing output of '{command}' with arguments '{args}' in working dir '{workingDir}' (Current is '{currentDir()}')")
    use proc = createProcess workingDir command args true
    proc.WaitForExit()

    match proc.ExitCode with
    | 0 -> Success (proc.StandardOutput.ReadToEnd(), proc.ExitCode)
    | _ -> Error (proc.StandardError.ReadToEnd(), proc.ExitCode)

let execConsole (workingDir: string) (command: string) (args: string) =
    try
        use proc = createProcess workingDir command args false
        proc.WaitForExit()
        proc.ExitCode
    with
        | exn -> forwardExternalError($"Process '{command}' with arguments '{args}' in directory '{workingDir}' failed", exn)

let execCaptureTimestampedOutput (workingDir: string) (command: string) (args: string) (logFile: string) =
    try
        use logWriter = new StreamWriter(logFile)
        let writeLock = obj()

        let inline lockWrite (from: string) (msg: string | null) =
            match msg with
            | NonNull msg -> 
                lock writeLock (fun () -> logWriter.WriteLine($"{DateTime.UtcNow} {from} {msg}"))
            | _ -> ()

        Log.Debug($"Running and capturing timestamped output of '{command}' with arguments '{args}' in working dir '{workingDir}' (Current is '{currentDir()}')")
        use proc = createProcess workingDir command args true
        proc.OutputDataReceived.Add(fun e -> lockWrite "OUT" e.Data)
        proc.ErrorDataReceived.Add(fun e -> lockWrite "ERR" e.Data)
        proc.BeginOutputReadLine()
        proc.BeginErrorReadLine()
        proc.WaitForExit()
        proc.ExitCode
    with
        | exn -> forwardExternalError($"Process '{command}' with arguments '{args}' in directory '{workingDir}' failed", exn)
