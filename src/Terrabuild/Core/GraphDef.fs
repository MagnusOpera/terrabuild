module GraphDef
open Collections

[<RequireQualifiedAccess>]
type ContaineredShellOperation = {
    Container: string option
    ContainerPlatform: string option
    ContainerVariables: string set
    MetaCommand: string
    Command: string
    Arguments: string
    ErrorLevel: int
}

[<RequireQualifiedAccess>]
type Cacheability =
    | Never
    | Local
    | External
    | Remote

[<RequireQualifiedAccess>]
type Rebuild =
    | Auto
    | Cascade
    | Always

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
    Cache: Cacheability
    Rebuild: Rebuild

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
    match node.Cache with
    | Cacheability.Remote
    | Cacheability.External -> options.LocalOnly |> not
    | _ -> false
