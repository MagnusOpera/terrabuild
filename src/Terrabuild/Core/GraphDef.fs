module GraphDef
open Collections

[<RequireQualifiedAccess>]
type ContaineredShellOperation = {
    Image: string option
    Platform: string option
    Variables: string set
    Envs: Map<string, string>
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
    | Always
    | Auto

// NOTE: order is important here, must be ordered by priority (last one wins)
//       Ignore has lower priority than Build for example
[<RequireQualifiedAccess>]
type NodeAction =
    | Ignore
    | Summary
    | Restore
    | Build

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
    Artifacts: Artifacts
    Build: Build

    Action: NodeAction
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
    | Artifacts.Managed
    | Artifacts.External -> options.LocalOnly |> not
    | _ -> false
