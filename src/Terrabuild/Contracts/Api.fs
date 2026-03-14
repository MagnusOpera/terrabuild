namespace Contracts
open System

type IApiClient =
    abstract StartBuild: Unit -> Unit
    abstract CompleteBuild: success:bool -> Unit
    abstract AddArtifact: project:string -> target:string -> projectHash:string -> targetHash:string  -> files:string list -> success:bool -> startedAt:DateTime -> endedAt:DateTime -> Unit
    abstract UseArtifact: projectHash:string -> hash:string -> Unit
    abstract GetArtifact: path:string -> Uri
