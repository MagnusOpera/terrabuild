module GraphDef
open Collections

[<RequireQualifiedAccess>]
type ContaineredShellOperation = {
    Image: string option
    Platform: string option
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
    | All

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
type Node = {
    Id: string

    ProjectId: string
    ProjectName: string option
    ProjectDir: string
    Target: string

    Dependencies: string set
    Outputs: string set

    ProjectHash: string
    TargetHash: string
    ClusterHash: string option

    Operations: ContaineredShellOperation list
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
}

let buildCacheKey (node: Node) = $"{node.ProjectHash}/{node.Target}/{node.TargetHash}"

let isRemoteCacheable (options: ConfigOptions.Options) (node: Node) = 
    match node.Artifacts with
    | ArtifactMode.Managed
    | ArtifactMode.External -> options.LocalOnly |> not
    | _ -> false
