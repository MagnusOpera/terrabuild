namespace Contracts
open System

type BuildGraphNode = {
    Id: string
    ProjectId: string
    ProjectName: string option
    ProjectDir: string
    Target: string
    ProjectHash: string
    TargetHash: string
    Dependencies: string list
    Artifacts: string
    Build: string
    Batch: string
    Action: string
    Required: bool
    IsBatchNode: bool
}

[<RequireQualifiedAccess>]
type CommitGraph = {
    Repository: string
    Commit: string
    GraphHash: string
    Nodes: BuildGraphNode list
}

type IApiClient =
    abstract StartBuild: Unit -> Unit
    abstract UploadBuildGraph: graphHash:string -> nodes:BuildGraphNode list -> Unit
    abstract CompleteBuild: success:bool -> Unit
    abstract AddArtifact: project:string -> projectName:string option -> target:string -> projectHash:string -> targetHash:string  -> files:string list -> success:bool -> startedAt:DateTime -> endedAt:DateTime -> Unit
    abstract UseArtifact: projectHash:string -> hash:string -> Unit
    abstract GetArtifact: path:string -> Uri
    abstract GetCommitGraph: repository:string -> commit:string -> CommitGraph
