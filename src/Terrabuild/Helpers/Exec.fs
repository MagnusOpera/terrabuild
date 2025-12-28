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
            if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
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




let private createProcess workingDir command args envs redirect =
    let psi = ProcessStartInfo (FileName = command,
                                Arguments = args,
                                UseShellExecute = false,
                                WorkingDirectory = workingDir,
                                RedirectStandardOutput = redirect,
                                RedirectStandardError = redirect)

    envs |> Map.iter (fun key value -> psi.EnvironmentVariables[key] <- value)

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

let execCaptureOutput (workingDir: string) (command: string) (args: string) (envs: Map<string, string>) =
    Log.Debug("Running and capturing output of '{Command}' with arguments '{Args}' in working dir '{WorkingDir}'", command, args, workingDir)
    use proc = createProcess workingDir command args envs true
    proc.WaitForExit()

    match proc.ExitCode with
    | 0 -> Success (proc.StandardOutput.ReadToEnd(), proc.ExitCode)
    | _ -> Error (proc.StandardError.ReadToEnd(), proc.ExitCode)

let execConsole (workingDir: string) (command: string) (args: string) (envs: Map<string, string>) =
    try
        use proc = createProcess workingDir command args envs false
        proc.WaitForExit()
        proc.ExitCode
    with
        | exn -> forwardExternalError($"Process '{command}' with arguments '{args}' in directory '{workingDir}' failed", exn)

let execCaptureTimestampedOutput (workingDir: string) (command: string) (args: string) (envs: Map<string, string>) (logFile: string) =
    try
        use logWriter = new StreamWriter(logFile)
        let writeLock = Lock()

        let inline lockWrite (from: string) (msg: string | null) =
            match msg with
            | NonNull msg -> lock writeLock (fun () -> logWriter.WriteLine($"{DateTime.UtcNow} {from} {msg}"))
            | _ -> ()

        Log.Debug("Running and capturing timestamped output of '{Command}' with arguments '{Args}' in working dir '{WorkingDir}'", command, args, workingDir)
        use proc = createProcess workingDir command args envs true
        proc.OutputDataReceived.Add(fun e -> lockWrite "OUT" e.Data)
        proc.ErrorDataReceived.Add(fun e -> lockWrite "ERR" e.Data)
        proc.BeginOutputReadLine()
        proc.BeginErrorReadLine()
        proc.WaitForExit()
        proc.ExitCode
    with
        | exn -> forwardExternalError($"Process '{command}' with arguments '{args}' in directory '{workingDir}' failed", exn)
