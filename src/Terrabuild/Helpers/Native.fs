module Native
open System
open System.Runtime.InteropServices

module Windows =
    open System.Diagnostics
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

    [<DllImport("libc", SetLastError = true)>]
    extern uint getuid()

    [<DllImport("libc", SetLastError = true)>]
    extern uint getgid()
