module internal CredentialStore

open System
open System.ComponentModel
open System.Diagnostics
open System.IO
open System.Runtime.InteropServices
open System.Security.AccessControl
open System.Security.Principal
open System.Security.Cryptography
open System.Text
open Errors

type Store = {
    Read: string -> string option
    Write: string -> string -> unit
    Delete: string -> unit
}

let private service = "io.magnusopera.terrabuild"

let deserialize<'value> serialized =
    try Json.Deserialize<'value> serialized
    with
    | :? System.Text.Json.JsonException
    | :? TerrabuildException ->
        raiseInvalidArg "Stored Terrabuild credentials are unreadable. Sign in again."

let identifier (value: string) =
    value |> Encoding.UTF8.GetBytes |> SHA256.HashData |> Convert.ToHexString

module private Mac =
    [<Literal>]
    let private Security = "/System/Library/Frameworks/Security.framework/Security"
    [<Literal>]
    let private CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation"

    [<DllImport(Security)>]
    extern int SecItemCopyMatching(nativeint query, nativeint& result)
    [<DllImport(Security)>]
    extern int SecItemAdd(nativeint query, nativeint result)
    [<DllImport(Security)>]
    extern int SecItemUpdate(nativeint query, nativeint attributes)
    [<DllImport(Security)>]
    extern int SecItemDelete(nativeint query)
    [<DllImport(CoreFoundation)>]
    extern nativeint CFDictionaryCreateMutable(nativeint allocator, nativeint capacity, nativeint keys, nativeint values)
    [<DllImport(CoreFoundation)>]
    extern void CFDictionarySetValue(nativeint dictionary, nativeint key, nativeint value)
    [<DllImport(CoreFoundation)>]
    extern nativeint CFStringCreateWithCString(nativeint allocator, [<MarshalAs(UnmanagedType.LPUTF8Str)>] string value, uint32 encoding)
    [<DllImport(CoreFoundation)>]
    extern nativeint CFDataCreate(nativeint allocator, byte[] value, nativeint length)
    [<DllImport(CoreFoundation)>]
    extern nativeint CFDataGetLength(nativeint data)
    [<DllImport(CoreFoundation)>]
    extern nativeint CFDataGetBytePtr(nativeint data)
    [<DllImport(CoreFoundation)>]
    extern void CFRelease(nativeint value)

    let private security = lazy NativeLibrary.Load(Security)
    let private foundation = lazy NativeLibrary.Load(CoreFoundation)
    let private constant name = Marshal.ReadIntPtr(NativeLibrary.GetExport(security.Value, name))

    let private withReference action reference =
        try action reference
        finally CFRelease(reference)

    let private withDictionary action =
        let keys = NativeLibrary.GetExport(foundation.Value, "kCFTypeDictionaryKeyCallBacks")
        let values = NativeLibrary.GetExport(foundation.Value, "kCFTypeDictionaryValueCallBacks")
        CFDictionaryCreateMutable(0n, 0n, keys, values) |> withReference action

    let private set dictionary key value =
        CFDictionarySetValue(dictionary, constant key, value)

    let private setString dictionary key value =
        CFStringCreateWithCString(0n, value, 0x08000100u) |> withReference (set dictionary key)

    let private withQuery name action =
        withDictionary (fun query ->
            set query "kSecClass" (constant "kSecClassGenericPassword")
            setString query "kSecAttrService" service
            setString query "kSecAttrAccount" name
            action query)

    let private setSecret dictionary (secret: string) =
        let bytes = Encoding.UTF8.GetBytes(secret)
        try
            CFDataCreate(0n, bytes, nativeint bytes.Length) |> withReference (set dictionary "kSecValueData")
        finally CryptographicOperations.ZeroMemory(bytes)

    let private check status =
        if status <> 0 then raiseExternalError $"macOS Keychain is unavailable or denied access ({status})."

    let read name =
        withQuery name (fun query ->
            let trueValue = Marshal.ReadIntPtr(NativeLibrary.GetExport(foundation.Value, "kCFBooleanTrue"))
            set query "kSecReturnData" trueValue
            let mutable result = 0n
            match SecItemCopyMatching(query, &result) with
            | -25300 -> None
            | status ->
                check status
                result |> withReference (fun data ->
                    let bytes = Array.zeroCreate<byte> (int (CFDataGetLength(data)))
                    Marshal.Copy(CFDataGetBytePtr(data), bytes, 0, bytes.Length)
                    try Some (Encoding.UTF8.GetString(bytes))
                    finally CryptographicOperations.ZeroMemory(bytes)))

    let write name secret =
        withQuery name (fun query ->
            withDictionary (fun attributes ->
                setSecret attributes secret
                match SecItemUpdate(query, attributes) with
                | -25300 ->
                    setSecret query secret
                    match SecItemAdd(query, 0n) with
                    | -25299 ->
                        // A concurrent login added the item after the update attempt.
                        withQuery name (fun existing -> SecItemUpdate(existing, attributes) |> check)
                    | status -> check status
                | status -> check status))

    let delete name =
        withQuery name (fun query ->
            match SecItemDelete(query) with
            | -25300 -> ()
            | status -> check status)

module private Windows =
    [<Struct; StructLayout(LayoutKind.Sequential)>]
    type Credential =
        val mutable Flags: uint32
        val mutable Type: uint32
        val mutable TargetName: nativeint
        val mutable Comment: nativeint
        val mutable LastWritten: System.Runtime.InteropServices.ComTypes.FILETIME
        val mutable BlobSize: uint32
        val mutable Blob: nativeint
        val mutable Persist: uint32
        val mutable AttributeCount: uint32
        val mutable Attributes: nativeint
        val mutable TargetAlias: nativeint
        val mutable UserName: nativeint

    [<DllImport("Advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)>]
    extern bool CredReadW(string target, uint32 credentialType, uint32 flags, nativeint& result)
    [<DllImport("Advapi32.dll", SetLastError = true)>]
    extern bool CredWriteW(Credential& credential, uint32 flags)
    [<DllImport("Advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)>]
    extern bool CredDeleteW(string target, uint32 credentialType, uint32 flags)
    [<DllImport("Advapi32.dll")>]
    extern void CredFree(nativeint value)

    let private target name = service + "/" + name
    let private generic = 1u
    let private notFound = 1168
    let private fail error = raiseExternalError $"Windows Credential Manager is unavailable or denied access ({error})."

    let read name =
        let mutable result = 0n
        if CredReadW(target name, generic, 0u, &result) then
            try
                let credential = Marshal.PtrToStructure(result, typeof<Credential>) |> unbox<Credential>
                let bytes = Array.zeroCreate<byte> (int credential.BlobSize)
                Marshal.Copy(credential.Blob, bytes, 0, bytes.Length)
                try Some (Encoding.UTF8.GetString(bytes))
                finally CryptographicOperations.ZeroMemory(bytes)
            finally CredFree(result)
        else
            let error = Marshal.GetLastPInvokeError()
            if error = notFound then None else fail error

    let write name (secret: string) =
        let bytes = Encoding.UTF8.GetBytes(secret)
        let targetName = Marshal.StringToCoTaskMemUni(target name)
        let blob = GCHandle.Alloc(bytes, GCHandleType.Pinned)
        try
            let mutable credential = Unchecked.defaultof<Credential>
            credential.Type <- generic
            credential.TargetName <- targetName
            credential.BlobSize <- uint32 bytes.Length
            credential.Blob <- blob.AddrOfPinnedObject()
            credential.Persist <- 2u // Persist on this machine for this Windows user.
            if not (CredWriteW(&credential, 0u)) then fail (Marshal.GetLastPInvokeError())
        finally
            CryptographicOperations.ZeroMemory(bytes)
            blob.Free()
            Marshal.FreeCoTaskMem(targetName)

    let delete name =
        if not (CredDeleteW(target name, generic, 0u)) then
            let error = Marshal.GetLastPInvokeError()
            if error <> notFound then fail error

module private Linux =
    let private invoke command name (input: string option) =
        let info = ProcessStartInfo("/usr/bin/secret-tool", UseShellExecute = false,
                                    RedirectStandardInput = true, RedirectStandardOutput = true,
                                    RedirectStandardError = true)
        info.ArgumentList.Add(command)
        if command = "store" then info.ArgumentList.Add("--label=Terrabuild")
        for argument in [ "service"; service; "account"; name ] do
            info.ArgumentList.Add(argument)
        use child =
            try Process.Start(info) |> nonNull
            with :? Win32Exception ->
                raiseExternalError "Linux Secret Service requires libsecret's secret-tool and an available keyring."
        let output = child.StandardOutput.ReadToEndAsync()
        let errors = child.StandardError.ReadToEndAsync()
        try input |> Option.iter child.StandardInput.Write
        finally child.StandardInput.Close()
        if not (child.WaitForExit(60000)) then
            child.Kill()
            raiseExternalError "Linux Secret Service timed out."
        let error = errors.GetAwaiter().GetResult()
        match child.ExitCode with
        | 0 -> Some (output.GetAwaiter().GetResult().TrimEnd('\r', '\n'))
        | 1 when input.IsNone && String.IsNullOrWhiteSpace(error) -> None
        | _ -> raiseExternalError "Linux Secret Service is unavailable or denied access. Unlock the desktop keyring and try again."

    let read name = invoke "lookup" name None
    let write name secret = invoke "store" name (Some secret) |> ignore
    let delete name = invoke "clear" name None |> ignore

let desktop () =
    if OperatingSystem.IsMacOS() then { Read = Mac.read; Write = Mac.write; Delete = Mac.delete }
    elif OperatingSystem.IsWindows() then { Read = Windows.read; Write = Windows.write; Delete = Windows.delete }
    elif OperatingSystem.IsLinux() then { Read = Linux.read; Write = Linux.write; Delete = Linux.delete }
    else raiseExternalError "Terrabuild has no OS credential store for this platform."

let job root =
    let path name = Path.Combine(root, identifier name)
    let write name (secret: string) =
        let temporary = Path.Combine(root, Guid.NewGuid().ToString("N"))
        let options = FileStreamOptions(Mode = FileMode.CreateNew, Access = FileAccess.Write)
        if not (OperatingSystem.IsWindows()) then
            options.UnixCreateMode <- Nullable (UnixFileMode.UserRead ||| UnixFileMode.UserWrite)
        try
            do
                use stream = new FileStream(temporary, options)
                use writer = new StreamWriter(stream)
                writer.Write(secret)
            File.Move(temporary, path name, true)
        finally File.Delete(temporary)

    { Read = fun name ->
          try Some (File.ReadAllText(path name))
          with
          | :? FileNotFoundException
          | :? DirectoryNotFoundException -> None
      Write = write
      Delete = fun name -> File.Delete(path name) }

let private jobRoot =
    lazy (
        let env name = Environment.GetEnvironmentVariable(name) |> Option.ofObj
        let root =
            match env "TERRABUILD_CI_CREDENTIAL_DIR", env "GITHUB_ACTIONS" with
            | Some path, _ -> Some path
            | None, Some "true" ->
                match env "RUNNER_TEMP" with
                | Some path -> Some path
                | None -> raiseExternalError "GitHub Actions did not provide RUNNER_TEMP."
            | _ -> None
        root |> Option.map (fun path ->
            if not (Path.IsPathFullyQualified(path)) then
                raiseInvalidArg "The CI credential directory must be an absolute private job directory."
            Path.Combine(path, "terrabuild-credentials")))

let initialize () =
    // Read job configuration before the workspace .env is loaded.
    jobRoot.Force() |> ignore

let isCi () = jobRoot.Value.IsSome

let private createJobDirectory root =
    if OperatingSystem.IsWindows() then
        use identity = WindowsIdentity.GetCurrent()
        let owner = identity.User |> nonNull
        let acl = DirectorySecurity()
        acl.SetAccessRuleProtection(true, false)
        acl.AddAccessRule(FileSystemAccessRule(owner, FileSystemRights.FullControl,
                                              InheritanceFlags.ContainerInherit ||| InheritanceFlags.ObjectInherit,
                                              PropagationFlags.None, AccessControlType.Allow))
        FileSystemAclExtensions.Create(DirectoryInfo(root), acl)
    else
        let ownerOnly = UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute
        Directory.CreateDirectory(root, ownerOnly) |> ignore
        if File.GetUnixFileMode(root) &&& (~~~ownerOnly) <> enum<UnixFileMode> 0 then
            raiseInvalidArg "The CI credential directory must be private to the runner user."
    if File.GetAttributes(root).HasFlag(FileAttributes.ReparsePoint) then
        raiseInvalidArg "The CI credential directory must not be a symbolic link."

let publisher () =
    match jobRoot.Value with
    | None -> desktop (), Path.Combine(Cache.createTerrabuildProfile(), "locks")
    | Some root ->
        createJobDirectory root
        job root, root
