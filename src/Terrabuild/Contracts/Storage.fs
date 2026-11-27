namespace Contracts

type IBatchStorage =
    abstract PrepareReads: entries:string list -> unit


type IStorage =
    abstract Exists: id:string -> bool
    abstract TryDownload: id:string -> string option
    abstract Upload: id:string -> summaryFile:string -> unit
    abstract Name: string


type internal IWorkspaceStorage =
    abstract BindBuild: getId:(unit -> string) -> unit
    abstract Commit: entry:string -> generation:string -> productionId:string option -> successful:bool -> unit
    abstract Abort: entry:string -> generation:string -> unit
