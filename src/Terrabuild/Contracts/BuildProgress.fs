
namespace BuildProgress




type IBuildProgress =
    abstract BuildStarted: unit -> unit
    abstract BuildCompleted: unit -> unit

    abstract TaskScheduled: taskId:string -> label:string -> unit
    abstract TaskDownloading: taskId:string -> unit
    abstract TaskBuilding: taskId:string -> unit
    abstract TaskUploading: taskId:string -> unit
    abstract TaskCompleted: taskId:string -> restore:bool -> success:bool -> unit
