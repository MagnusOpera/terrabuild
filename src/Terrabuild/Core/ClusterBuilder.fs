
module ClusterBuilder
open Collections
open GraphDef
open Errors


type Cluster =
    { Id: string
      Nodes: Set<string> }

type ClusterGraph =
    { Clusters: Cluster list
      Edges: Set<string * string> }

/// A simple union–find structure
type UnionFind<'T when 'T : comparison>(elements: seq<'T>) =
    let parent = elements |> Seq.map (fun x -> x, x) |> Map.ofSeq |> ref

    let rec find x =
        let p = parent.Value[x]
        if p = x then x
        else
            let root = find p
            parent.Value <- parent.Value.Add(x, root)
            root

    member _.Find(x) = find x

    member _.Union(x, y) =
        let rx, ry = find x, find y
        if rx <> ry then parent.Value <- parent.Value.Add(rx, ry)

    member _.Groups() =
        parent.Value
        |> Map.toSeq
        |> Seq.groupBy (fun (x, _) -> find x)
        |> Seq.map (fun (root, items) -> root, items |> Seq.map fst |> Set.ofSeq)

let computeClusters (graph: Graph) : ClusterGraph =
    let uf = UnionFind(graph.Nodes |> Map.keys)

    // 1) Union nodes with the same ClusterId (global grouping)
    graph.Nodes.Values
    |> Seq.map (fun n -> n.ClusterHash, n.Id)
    |> Seq.groupBy fst
    |> Seq.iter (fun (_, group) ->
        match Seq.toList (Seq.map snd group) with
        | [] | [_] -> ()
        | first :: rest -> for n in rest do uf.Union(first, n))

    // --- Build clusters ---
    let groupMap =
        uf.Groups()
        |> Seq.map (fun (parent, nodes) ->
            let cid = graph.Nodes[parent].ClusterHash
            cid, nodes)
        |> Map.ofSeq

    let clusters =
        groupMap
        |> Seq.map (fun kv -> { Id = kv.Key; Nodes = kv.Value })
        |> Seq.toList

    // --- Build edges between clusters ---
    let nodeToCluster =
        groupMap
        |> Seq.collect (fun (KeyValue(cid, nodes)) ->
            nodes |> Seq.map (fun n -> n, cid))
        |> Map.ofSeq

    let edges =
        graph.Nodes.Values
        |> Seq.collect (fun node ->
            node.Dependencies
            |> Seq.map (fun depId ->
                let c1 = nodeToCluster[node.Id]
                let c2 = nodeToCluster[depId]
                if c1 <> c2 then Some(c1, c2) else None))
        |> Seq.choose id
        |> Set.ofSeq

    { Clusters = clusters; Edges = edges }



let createClusterNodes (options: ConfigOptions.Options) (configuration: Configuration.Workspace) (graph: GraphDef.Graph) =
    graph.Clusters
    |> Map.choose (fun clusterHash cluster ->
        let nodeIds = cluster.Nodes |> Set.toList
        match nodeIds with
        | [] | [_] -> None // skip clusters with 0 or 1 node
        | headNodeId :: _ ->
            let headNode = graph.Nodes[headNodeId]
            let projectDirs =
                nodeIds
                |> List.choose (fun nid -> graph.Nodes |> Map.tryFind nid |> Option.map (fun n -> n.ProjectDir))
            let batchContext =
                Some {
                    Terrabuild.Extensibility.BatchContext.Hash = clusterHash
                    Terrabuild.Extensibility.BatchContext.TempDir = options.SharedDir
                    Terrabuild.Extensibility.BatchContext.ProjectPaths = projectDirs
                }
            let projectId = headNode.ProjectDir |> String.toLower
            let projectConfig = configuration.Projects[projectId]
            let targetConfig = projectConfig.Targets[headNode.Target]
            let ops =
                targetConfig.Operations
                |> List.collect (fun operation ->
                    let optContext =
                        { Terrabuild.Extensibility.ActionContext.Debug = options.Debug
                          Terrabuild.Extensibility.ActionContext.CI = options.Run.IsSome
                          Terrabuild.Extensibility.ActionContext.Command = operation.Command
                          Terrabuild.Extensibility.ActionContext.Hash = clusterHash
                          Terrabuild.Extensibility.ActionContext.Batch = batchContext }
                    let parameters =
                        match operation.Context with
                        | Terrabuild.Expressions.Value.Map map ->
                            map
                            |> Map.add "context" (Terrabuild.Expressions.Value.Object optContext)
                            |> Terrabuild.Expressions.Value.Map
                        | _ -> raiseBugError "Failed to get context (internal error)"
                    match Extensions.invokeScriptMethod<Terrabuild.Extensibility.ShellOperations> optContext.Command parameters (Some operation.Script) with
                    | Extensions.InvocationResult.Success executionRequest ->
                        executionRequest |> List.map (fun shellOperation -> {
                            ContaineredShellOperation.Container = operation.Container
                            ContaineredShellOperation.ContainerPlatform = operation.Platform
                            ContaineredShellOperation.ContainerVariables = operation.ContainerVariables
                            ContaineredShellOperation.MetaCommand = $"{operation.Extension} {operation.Command}"
                            ContaineredShellOperation.Command = shellOperation.Command
                            ContaineredShellOperation.Arguments = shellOperation.Arguments |> String.normalizeShellArgs })
                    | Extensions.InvocationResult.ErrorTarget ex ->
                        forwardExternalError($"{clusterHash}: Failed to get shell operation (extension error)", ex)
                    | _ -> raiseExternalError $"{clusterHash}: Failed to get shell operation (extension error)"
                )
            let clusterNode =
                { GraphDef.Node.Id = clusterHash
                  GraphDef.Node.ProjectId = None
                  GraphDef.Node.ProjectDir = "."
                  GraphDef.Node.Target = headNode.Target
                  GraphDef.Node.Operations = ops
                  GraphDef.Node.Cache = headNode.Cache
                  GraphDef.Node.Dependencies = cluster.Edges
                  GraphDef.Node.Outputs = Set.empty
                  GraphDef.Node.ClusterHash = clusterHash
                  GraphDef.Node.ProjectHash = clusterHash
                  GraphDef.Node.TargetHash = headNode.TargetHash
                  GraphDef.Node.IsLeaf = headNode.IsLeaf
                  GraphDef.Node.Action = NodeAction.BatchBuild }
            Some clusterNode
    )



let build (options: ConfigOptions.Options) (configuration: Configuration.Workspace) (graph: GraphDef.Graph) =
    let clusterGraph = computeClusters graph
    let clusters =
        clusterGraph.Clusters 
        |> Seq.map (fun cluster -> cluster.Id, cluster.Nodes)
        |> Map.ofSeq

    let edges =
        clusterGraph.Edges
        |> Seq.groupBy fst
        |> Seq.map (fun (fromCluster, toClusters) -> fromCluster, toClusters |> Seq.map snd |> Set.ofSeq)
        |> Map.ofSeq

    let clusters =
        clusters
        |> Map.map (fun clusterId nodes ->
            { GraphDef.Cluster.Nodes = nodes
              GraphDef.Cluster.Edges = edges |> Map.tryFind clusterId |> Option.defaultValue Set.empty })

    let graph =
        { graph with
            GraphDef.Graph.Clusters = clusters }

    let clusterNodes = createClusterNodes options configuration graph
    let graph =
        { graph with
            GraphDef.Graph.Nodes = graph.Nodes |> Map.addMap clusterNodes }

    graph

