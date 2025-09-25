
module ClusterBuilder
open Collections
open GraphDef


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

let build (graph: GraphDef.Graph) =
    let clusterGraph = computeClusters graph
    let clusters =
        clusterGraph.Clusters 
        |> Seq.map (fun cluster -> cluster.Id, cluster.Nodes)
        |> Map.ofSeq

    let edges =
        clusterGraph.Edges
        |> Seq.groupBy (fun (fromCluster, toCluster) -> fromCluster)
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

    graph
