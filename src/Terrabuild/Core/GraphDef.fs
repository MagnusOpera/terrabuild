module GraphDef
open Collections

[<RequireQualifiedAccess>]
type ContaineredShellOperation = {
    Image: string option
    Platform: string option
    Variables: string set
    MetaCommand: string
    Command: string
    Arguments: string
    ErrorLevel: int
}

[<RequireQualifiedAccess>]
type Artifacts =
    | None
    | Workspace
    | Managed
    | External

[<RequireQualifiedAccess>]
type Build =
    | Auto
    | Always
    | Cascade

[<RequireQualifiedAccess>]
type NodeAction =
    | BatchBuild
    | Build
    | Restore
    | Summary
    | Ignore

[<RequireQualifiedAccess>]
type Node = {
    Id: string

    ProjectId: string option
    ProjectDir: string
    Target: string

    Dependencies: string set
    Outputs: string set

    ProjectHash: string
    TargetHash: string
    ClusterHash: string

    Operations: ContaineredShellOperation list
    Artifacts: Artifacts
    Build: Build

    Action: NodeAction
}


[<RequireQualifiedAccess>]
type Graph = {
    Nodes: Map<string, Node> // node to Node definition
    RootNodes: string set // nodeId of root nodes
    Clusters: Map<string, string set>
}

let buildCacheKey (node: Node) = $"{node.ProjectHash}/{node.Target}/{node.TargetHash}"

let isRemoteCacheable (options: ConfigOptions.Options) (node: Node) = 
    match node.Artifacts with
    | Artifacts.Managed
    | Artifacts.External -> options.LocalOnly |> not
    | _ -> false
