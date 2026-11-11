module GraphDef
open Collections
open System
open System.Runtime.InteropServices
open System.Text.Json.Serialization
open Terrabuild.ScriptingContracts

[<RequireQualifiedAccess>]
type OperationPlatform =
    | Portable
    | Sensitive of effectivePlatform: string option
    | Unknown of effectivePlatform: string option

[<RequireQualifiedAccess>]
type ContaineredShellOperation = {
    Image: string option
    Platform: string option
    [<JsonIgnore>]
    CachePlatform: OperationPlatform
    Cpus: int option
    Variables: string set
    Envs: Map<string, string>
    MetaCommand: string
    Command: string
    Arguments: string
    ErrorLevel: int
}

[<RequireQualifiedAccess>]
type BatchMode =
    | Never
    | Partition
    | Single

[<RequireQualifiedAccess>]
type ArtifactMode =
    | None
    | Workspace
    | Managed
    | External

[<RequireQualifiedAccess>]
type BuildMode =
    | Lazy
    | Auto
    | Always

[<RequireQualifiedAccess>]
type RunAction =
    | Ignore
    | Summary
    | Restore
    | Exec

[<RequireQualifiedAccess>]
type EvaluationInput = {
    Name: string
    ValueHash: string
}

[<RequireQualifiedAccess>]
type Node = {
    Id: string

    ProjectId: string
    ProjectName: string option
    ProjectDir: string
    Target: string
    Phase: string option
    Locks: string set

    Dependencies: string set
    [<JsonIgnore>]
    PhaseDependencies: string set
    Outputs: string set

    ProjectHash: string
    TargetHash: string
    ClusterHash: string option
    [<JsonIgnore>]
    CanReuseCache: bool

    [<JsonIgnore>]
    Provenance: Contracts.ProvenancePlan option
    Operations: ContaineredShellOperation list
    EvaluationInputs: EvaluationInput list
    EnvironmentSensitive: bool option
    Artifacts: ArtifactMode
    Build: BuildMode
    Batch: BatchMode
    Action: RunAction
    Required: bool
}


[<RequireQualifiedAccess>]
type Graph = {
    Nodes: Map<string, Node> // node to Node definition
    RootNodes: string set // nodeId of root nodes
    Batches: Map<string, string set>
    Phases: Map<string, string set>
}

let buildCacheKey (node: Node) = $"{node.ProjectHash}/{node.Target}/{node.TargetHash}"

/// Fingerprint declared host environment inputs without retaining their values.
let forwardedVariablesFingerprint variables =
    Environment.matchingEnvVars variables
    |> Seq.collect (fun (KeyValue(name, value)) -> [ name; value ])
    |> Hash.sha256strings

let internal resolveOperationPlatform hostPlatform engine image (containerPlatform: string option) sensitivity =
    let effectivePlatform () =
        match engine, image with
        | ConfigOptions.Engine.Host, _
        | _, None -> hostPlatform |> Option.map (fun platform -> $"host:{platform}")
        | _ ->
            containerPlatform
            |> Option.bind (fun platform ->
                match platform.Split('/') with
                | [| os; architecture |]
                | [| os; architecture; _ |] when not (String.IsNullOrWhiteSpace(os) || String.IsNullOrWhiteSpace(architecture)) ->
                    Some $"container:{platform}"
                | _ -> None)

    match sensitivity with
    | Some PlatformSensitivity.Portable -> OperationPlatform.Portable
    | Some PlatformSensitivity.Sensitive -> OperationPlatform.Sensitive (effectivePlatform ())
    | None -> OperationPlatform.Unknown (effectivePlatform ())

let internal hostCachePlatform () =
    let architecture =
        match RuntimeInformation.OSArchitecture with
        | Architecture.X64 -> "amd64"
        | Architecture.X86 -> "386"
        | architecture -> architecture.ToString().ToLowerInvariant()
    match Environment.detectHostPlatform () with
    | Environment.HostPlatform.Linux -> Some $"linux/{architecture}"
    | Environment.HostPlatform.MacOS -> Some $"darwin/{architecture}"
    | Environment.HostPlatform.Windows -> Some $"windows/{architecture}"
    | Environment.HostPlatform.Other -> None

let operationHasKnownPlatform (operation: ContaineredShellOperation) =
    match operation.CachePlatform with
    | OperationPlatform.Portable
    | OperationPlatform.Sensitive (Some _)
    | OperationPlatform.Unknown (Some _) -> true
    | OperationPlatform.Sensitive None
    | OperationPlatform.Unknown None -> false

let operationPlatformInput (operation: ContaineredShellOperation) =
    match operation.CachePlatform with
    | OperationPlatform.Portable -> None
    | OperationPlatform.Sensitive platform
    | OperationPlatform.Unknown platform ->
        let value = platform |> Option.defaultValue "unknown"
        Some $"execution-platform:v1:{value}"

let operationCacheInput (operation: ContaineredShellOperation) =
    [ yield Json.Serialize operation
      if not (Set.isEmpty operation.Variables) then
          yield forwardedVariablesFingerprint operation.Variables
      yield! operationPlatformInput operation |> Option.toList ]
    |> String.join "\n"

let operationCacheHash operation =
    operationCacheInput operation |> Hash.sha256

let environmentSensitiveInputNames =
    Set [
        "terrabuild.environment"
        "terrabuild.branch_or_tag"
        "terrabuild.head_commit"
        "terrabuild.ci"
        "terrabuild.tag"
        "terrabuild.note"
        "terrabuild.group"
    ]

let environmentSensitiveInputs (inputs: EvaluationInput seq) =
    inputs
    |> Seq.filter (fun input -> environmentSensitiveInputNames |> Set.contains input.Name)
    |> Seq.sortBy _.Name
    |> List.ofSeq

let environmentSensitivityStatus environmentSensitive inputs =
    match environmentSensitive, environmentSensitiveInputs inputs with
    | Some true, [] -> "opted-in-unused"
    | Some true, _ -> "opted-in"
    | Some false, []
    | None, [] -> "neutral"
    | Some false, _ -> "declared-neutral"
    | None, _ -> "missing-opt-in"

let isRemoteCacheable (options: ConfigOptions.Options) (node: Node) = 
    match node.Artifacts with
    | ArtifactMode.Managed
    | ArtifactMode.External -> node.CanReuseCache && not options.LocalOnly
    | _ -> false
