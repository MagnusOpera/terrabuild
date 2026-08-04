namespace Storages
open System


type internal IRemoteStorageBackend =
    abstract Exists: id:string -> location:Uri -> bool
    abstract TryDownload: id:string -> location:Uri -> string option
    abstract Upload: id:string -> location:Uri -> summaryFile:string -> unit
