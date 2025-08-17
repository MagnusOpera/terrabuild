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
}

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
    Operations: ContaineredShellOperation list
    Cache: Terrabuild.Extensibility.Cacheability
    Rebuild: bool
    Idempotent: bool

    // tell if a node is leaf (that is no dependencies in same project)
    IsLeaf: bool
}


[<RequireQualifiedAccess>]
type Graph = {
    Nodes: Map<string, Node>
    RootNodes: string set
}


let buildCacheKey (node: Node) = $"{node.ProjectHash}/{node.Target}/{node.TargetHash}"
