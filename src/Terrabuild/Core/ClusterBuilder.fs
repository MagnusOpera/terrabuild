
module ClusterBuilder
open GraphDef
open Collections


type Cluster =
    { Id: string
      Nodes: Set<string> }

let computeClusters (graph: Graph) =
    let mutable clusterIdCounter = 0
    let nextClusterId () =
        clusterIdCounter <- clusterIdCounter + 1
        $"cluster-{clusterIdCounter}"

    let mutable nodeToCluster = Map.empty
    let mutable clusters = Map.empty

    let merge cid dependencies clusterHash =
        let mutable cluster = Set.empty
        for depId in dependencies do
            let depNode = graph.Nodes[depId]
            let cidDep = nodeToCluster[depId]
            if depNode.ClusterHash = clusterHash && cid <> cidDep then
                for nid in clusters[cidDep] do
                    nodeToCluster <- nodeToCluster |> Map.add nid cid
                    cluster <- cluster |> Set.add nid
                clusters <- clusters |> Map.remove cidDep
        cluster

    let rec visit nodeId =
        let node = graph.Nodes[nodeId]
        if not (nodeToCluster.ContainsKey nodeId) && node.Action = NodeAction.Build then
            for depId in node.Dependencies do
                visit depId
            let cid = nextClusterId()
            let cluster = merge cid node.Dependencies node.ClusterHash |> Set.add nodeId
            nodeToCluster <- nodeToCluster |> Map.add nodeId cid
            clusters <- clusters |> Map.add cid cluster

    for root in graph.RootNodes do visit root
    let rootHashs = graph.RootNodes |> Set.map (fun rootId -> graph.Nodes[rootId].ClusterHash)
    for rootHash in rootHashs do
        let cid = nextClusterId()
        let cluster = merge cid graph.RootNodes rootHash
        nodeToCluster <- nodeToCluster |> Map.add cid cid
        clusters <- clusters |> Map.add cid cluster

    // Build final immutable representation
    let clusters =
        clusters
        |> Seq.map (fun kvp -> { Id = kvp.Key; Nodes = Set.ofSeq kvp.Value })
        |> Seq.toList
    // clusters

    printfn $"All clusters:"
    for cluster in clusters do
        printfn $"Cluster: {cluster.Id}"
        for node in cluster.Nodes do
            printfn $"  {node}"

