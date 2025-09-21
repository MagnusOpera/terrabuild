module GraphDef
open Collections
open System

[<RequireQualifiedAccess>]
type ContaineredShellOperation = {
    Container: string option
    ContainerPlatform: string option
    ContainerVariables: string set
    MetaCommand: string
    Command: string
    Arguments: string
}

[<RequireQualifiedAccess>]
type NodeAction =
    | Build
    | Restore
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
    Cache: Terrabuild.Extensibility.Cacheability

    IsLeaf: bool // tell if a node is leaf (that is no dependencies in same project)

    Action: NodeAction
}


[<RequireQualifiedAccess>]
type Graph = {
    Nodes: Map<string, Node> // node to Node definition
    RootNodes: string set // nodeId of root nodes

    Clusters: Map<string, Set<string>> // clusterId to set of nodeId
    Node2Cluster: Map<string, string> // nodeId to clusterId
}


let buildCacheKey (node: Node) = $"{node.ProjectHash}/{node.Target}/{node.TargetHash}"
