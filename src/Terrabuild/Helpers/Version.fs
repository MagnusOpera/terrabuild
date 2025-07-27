
module Version
open System.Reflection

let informalVersion () =
    match Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>() with
    | null -> "0.0.0"
    | fileVersion -> fileVersion.InformationalVersion

let version () =
    informalVersion().Split("+")[0]
