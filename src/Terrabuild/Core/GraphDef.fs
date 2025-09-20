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

    Lineage: string
    ProjectHash: string
    TargetHash: string
    Operations: ContaineredShellOperation list
    Cache: Terrabuild.Extensibility.Cacheability
    Rebuild: bool

    // tell if a node is leaf (that is no dependencies in same project)
    IsLeaf: bool

    Action: NodeAction
}


[<RequireQualifiedAccess>]
type Graph = {
    Nodes: Map<string, Node>
    RootNodes: string set
}


let buildCacheKey (node: Node) = $"{node.ProjectHash}/{node.Target}/{node.TargetHash}"

