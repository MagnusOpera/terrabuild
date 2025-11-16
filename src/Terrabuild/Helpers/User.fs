
module User
open System.Runtime.InteropServices

let getUidGid() =
    if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
        None
    elif RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
         RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
        Some (Native.Posix.getuid(), Native.Posix.getgid())
    else
        None
