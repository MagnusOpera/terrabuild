namespace Contracts
open System

type BuildGraphNode = {
    Id: string
    ProjectId: string
    ProjectName: string option
    ProjectDir: string
    Target: string
    Phase: string option
    ProjectHash: string
    TargetHash: string
    Dependencies: string list
    PhaseDependencies: string list
    Transformations: ProvenanceTransformation list
    TargetHashResolved: bool option
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

type ArtifactUnavailableException(path: string) =
    inherit Exception($"Insights has no finalized artifact for {path}.")

type IApiClient =
    abstract StartBuild: Unit -> Unit
    abstract UploadBuildGraph: graphHash:string -> environment:string -> nodes:BuildGraphNode list -> Unit
    abstract CompleteBuild: success:bool -> Unit
    abstract AddArtifact: project:string -> projectName:string option -> target:string -> projectHash:string -> targetHash:string  -> files:string list -> success:bool -> startedAt:DateTime -> endedAt:DateTime -> provenance:ProvenanceDeclaration option -> Unit
    abstract UseArtifact: projectHash:string -> hash:string -> productionId:string option -> Unit
    abstract GetArtifact: path:string -> operation:string -> Uri * string option
    abstract GetCommitGraph: repository:string -> commit:string -> environment:string -> CommitGraph
