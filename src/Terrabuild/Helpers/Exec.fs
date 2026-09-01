module Exec
open System.Diagnostics
open System
open System.IO
open Errors
open Serilog
open Environment
open System.Runtime.InteropServices
open System.Collections.Concurrent
open System.Threading
open System.Text
open Lock



// ----------------------
// Native interop
// ----------------------
module Native =
    module Windows =
        [<DllImport("kernel32.dll", SetLastError = true)>]
        extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string|null lpName)

        [<DllImport("kernel32.dll", SetLastError = true)>]
        extern bool SetInformationJobObject(IntPtr hJob, int jobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength)

        [<DllImport("kernel32.dll", SetLastError = true)>]
        extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess)

        [<Struct; StructLayout(LayoutKind.Sequential)>]
        type JOBOBJECT_BASIC_LIMIT_INFORMATION =
            struct
                val mutable PerProcessUserTimeLimit: int64
                val mutable PerJobUserTimeLimit: int64
                val mutable LimitFlags: uint32
                val mutable MinimumWorkingSetSize: UIntPtr
                val mutable MaximumWorkingSetSize: UIntPtr
                val mutable ActiveProcessLimit: uint32
                val mutable Affinity: UIntPtr
                val mutable PriorityClass: uint32
                val mutable SchedulingClass: uint32
            end

        [<Struct; StructLayout(LayoutKind.Sequential)>]
        type IO_COUNTERS =
            struct
                val mutable ReadOperationCount: uint64
                val mutable WriteOperationCount: uint64
                val mutable OtherOperationCount: uint64
                val mutable ReadTransferCount: uint64
                val mutable WriteTransferCount: uint64
                val mutable OtherTransferCount: uint64
            end

        [<Struct; StructLayout(LayoutKind.Sequential)>]
        type JOBOBJECT_EXTENDED_LIMIT_INFORMATION =
            struct
                val mutable BasicLimitInformation: JOBOBJECT_BASIC_LIMIT_INFORMATION
                val mutable IoInfo: IO_COUNTERS
                val mutable ProcessMemoryLimit: UIntPtr
                val mutable JobMemoryLimit: UIntPtr
                val mutable PeakProcessMemoryUsed: UIntPtr
                val mutable PeakJobMemoryUsed: UIntPtr
            end

        let JobObjectExtendedLimitInformation = 9
        let JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000u

        // One job handle for the whole app
        let private jobHandle =
            lazy (
                let h = CreateJobObject(IntPtr.Zero, null)
                if h = IntPtr.Zero then failwith "Failed to create Job Object"

                let mutable info = JOBOBJECT_EXTENDED_LIMIT_INFORMATION()
                info.BasicLimitInformation.LimitFlags <- JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE

                let ptr = Marshal.AllocHGlobal(Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>())
                Marshal.StructureToPtr(info, ptr, false)

                if not (SetInformationJobObject(h, JobObjectExtendedLimitInformation, ptr, uint32 (Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>()))) then
                    failwith "Failed to configure Job Object"

                Marshal.FreeHGlobal ptr
                h
            )

        let assign (proc: Process) =
            if isWindowsHost () then
                let hJob = jobHandle.Value
                if not (AssignProcessToJobObject(hJob, proc.Handle)) then
                    failwithf "Failed to assign process %d to Job Object" proc.Id

    module Posix =
        [<DllImport("libc", SetLastError = true)>]
        extern int setpgid(int pid, int pgid)

// ----------------------
// Track all children
// ----------------------
let private children = ConcurrentBag<Process>()
let private containers = ConcurrentDictionary<int, string * string * IDisposable>()

[<RequireQualifiedAccess>]
type private ContainerRecord = {
    Engine: string
    Name: string
}

type private ContainerRecordLease(stream: FileStream, path: string) =
    interface IDisposable with
        member _.Dispose() =
            stream.Dispose()
            try File.Delete(path)
            with
            | :? FileNotFoundException
            | :? DirectoryNotFoundException -> ()

[<RequireQualifiedAccess>]
type Arguments =
    | Raw of string
    | List of string list

let private containerRecordsDirectory profile = FS.combinePath profile "containers"

let internal registerContainerRecordAt profile engine name =
    let directory = containerRecordsDirectory profile
    IO.createDirectory directory
    let path = FS.combinePath directory $"{name}.json"
    let stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None)
    try
        use writer = new StreamWriter(stream, Encoding.UTF8, 1024, true)
        { ContainerRecord.Engine = engine; ContainerRecord.Name = name }
        |> Json.Serialize
        |> writer.Write
        writer.Flush()
        stream.Flush(true)
        new ContainerRecordLease(stream, path) :> IDisposable
    with _ ->
        stream.Dispose()
        IO.deleteAny path
        reraise()

let internal reapContainerRecordsAt profile remove =
    let directory = containerRecordsDirectory profile
    let mutable reaped = 0
    if Directory.Exists directory then
        for path in Directory.EnumerateFiles(directory, "*.json") do
            try
                use stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
                use reader = new StreamReader(stream, Encoding.UTF8, true, 1024, true)
                let record = reader.ReadToEnd() |> Json.Deserialize<ContainerRecord>
                remove record.Engine record.Name
                stream.Dispose()
                File.Delete(path)
                reaped <- reaped + 1
            with
            | :? IOException -> ()
            | exn -> Log.Warning(exn, "Failed to reap abandoned container record {ContainerRecord}", path)
    reaped

let private forceRemoveContainer engine name =
    let psi = ProcessStartInfo(FileName = engine, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true)
    psi.ArgumentList.Add("rm")
    psi.ArgumentList.Add("-f")
    psi.ArgumentList.Add(name)
    use proc = new Process(StartInfo = psi)
    if proc.Start() then
        let stdout = proc.StandardOutput.ReadToEndAsync()
        let stderr = proc.StandardError.ReadToEndAsync()
        if not (proc.WaitForExit(10000)) then
            proc.Kill(true)
            proc.WaitForExit()
        stdout.GetAwaiter().GetResult() |> ignore
        stderr.GetAwaiter().GetResult() |> ignore

let reapContainers () =
    let profile = FS.combinePath ("HOME" |> Environment.envVar |> Option.get) ".terrabuild"
    reapContainerRecordsAt profile forceRemoveContainer
    |> ignore

let private tryContainerIdentity command arguments =
    match command, arguments with
    | ("docker" | "podman"), Arguments.List args ->
        args
        |> List.windowed 2
        |> List.tryPick (function
            | [ "--name"; name ] -> Some (command, name)
            | _ -> None)
    | _ -> None

let renderArguments = function
    | Arguments.Raw args -> args
    | Arguments.List args ->
        args
        |> List.map (fun arg ->
            if arg = "" || arg |> Seq.exists Char.IsWhiteSpace then
                let escaped = arg.Replace("\\", "\\\\").Replace("\"", "\\\"")
                $"\"{escaped}\""
            else arg)
        |> String.join " "



let private createProcess workingDir command arguments envs redirect =
    let psi = ProcessStartInfo (FileName = command,
                                UseShellExecute = false,
                                WorkingDirectory = workingDir,
                                RedirectStandardOutput = redirect,
                                RedirectStandardError = redirect)

    match arguments with
    | Arguments.Raw args -> psi.Arguments <- args
    | Arguments.List args -> args |> List.iter psi.ArgumentList.Add

    envs |> Map.iter (fun key value -> psi.EnvironmentVariables[key] <- value)

    let containerRecord =
        tryContainerIdentity command arguments
        |> Option.map (fun (engine, name) ->
            let profile = FS.combinePath ("HOME" |> Environment.envVar |> Option.get) ".terrabuild"
            engine, name, registerContainerRecordAt profile engine name)

    let proc = new Process(StartInfo = psi)

    try
        if not (proc.Start()) then
            failwithf "Failed to start process: %s" command
    with _ ->
        containerRecord |> Option.iter (fun (_, _, lease) -> lease.Dispose())
        reraise()

    children.Add proc

    containerRecord
    |> Option.iter (fun (engine, name, lease) ->
        containers[proc.Id] <- (engine, name, lease)
        let release () =
            try
                if proc.ExitCode <> 0 then forceRemoveContainer engine name
                match containers.TryRemove(proc.Id) with
                | true, (_, _, lease) -> lease.Dispose()
                | _ -> ()
            with exn ->
                Log.Warning(exn, "Failed to remove exited container {ContainerName} with {ContainerEngine}", name, engine)
        proc.EnableRaisingEvents <- true
        proc.Exited.Add(fun _ -> release ())
        if proc.HasExited then release ())

    if isWindowsHost () then
        Native.Windows.assign proc
    elif isPosixHost () then
        // Put child in its own process group
        Native.Posix.setpgid(proc.Id, 0) |> ignore

    proc



// ----------------------
// Cleanup hooks
// ----------------------
let cleanup () =
    // Stop daemon-owned containers before killing their local CLI processes.
    for KeyValue(processId, (engine, name, lease)) in containers do
        try forceRemoveContainer engine name
        with _ -> ()
        match containers.TryRemove(processId) with
        | true, (_, _, trackedLease) -> trackedLease.Dispose()
        | _ -> lease.Dispose()

    // As a fallback, ensure tracked children are killed
    for proc in children do
        try
            if not proc.HasExited then
                proc.Kill(true)   // Kill entire tree
        with _ -> ()



type CaptureResult =
    | Success of string*int
    | Error of string*int

let execCaptureOutput (workingDir: string) (command: string) (args: string) (envs: Map<string, string>) =
    Log.Debug("Running and capturing output of '{Command}' with arguments '{Args}' in working dir '{WorkingDir}'", command, args, workingDir)
    use proc = createProcess workingDir command (Arguments.Raw args) envs true
    let stdout = proc.StandardOutput.ReadToEndAsync()
    let stderr = proc.StandardError.ReadToEndAsync()
    proc.WaitForExit()
    let stdout = stdout.GetAwaiter().GetResult()
    let stderr = stderr.GetAwaiter().GetResult()

    match proc.ExitCode with
    | 0 -> Success (stdout, proc.ExitCode)
    | _ -> Error (stderr, proc.ExitCode)

let execConsoleArguments (workingDir: string) (command: string) arguments (envs: Map<string, string>) =
    let args = renderArguments arguments
    try
        use proc = createProcess workingDir command arguments envs false
        proc.WaitForExit()
        proc.ExitCode
    with
        | exn -> forwardExternalError($"Process '{command}' with arguments '{args}' in directory '{workingDir}' failed", exn)

let execConsole (workingDir: string) (command: string) (args: string) (envs: Map<string, string>) =
    execConsoleArguments workingDir command (Arguments.Raw args) envs

let execCaptureTimestampedOutputArguments (workingDir: string) (command: string) arguments (envs: Map<string, string>) (logFile: string) captureStdout =
    let args = renderArguments arguments
    try
        use logWriter = new StreamWriter(logFile)
        let stdout = if captureStdout then Some (StringBuilder()) else None
        let writeLock = Lock()
        let lockWrite (capture: bool) (msg: string | null) =
            match msg with
            | NonNull msg ->
                lock writeLock (fun () ->
                    logWriter.WriteLine(msg)
                    if capture then
                        stdout |> Option.iter (fun output -> output.AppendLine(msg) |> ignore)
                )
            | _ -> ()

        Log.Debug("Running and capturing timestamped output of '{Command}' with arguments '{Args}' in working dir '{WorkingDir}'", command, args, workingDir)
        use proc = createProcess workingDir command arguments envs true
        proc.OutputDataReceived.Add(fun e -> lockWrite true e.Data)
        proc.ErrorDataReceived.Add(fun e -> lockWrite false e.Data)
        proc.BeginOutputReadLine()
        proc.BeginErrorReadLine()
        proc.WaitForExit()
        proc.ExitCode, (stdout |> Option.map string)
    with
        | exn -> forwardExternalError($"Process '{command}' with arguments '{args}' in directory '{workingDir}' failed", exn)

let execCaptureTimestampedOutput (workingDir: string) (command: string) (args: string) (envs: Map<string, string>) (logFile: string) captureStdout =
    execCaptureTimestampedOutputArguments workingDir command (Arguments.Raw args) envs logFile captureStdout
