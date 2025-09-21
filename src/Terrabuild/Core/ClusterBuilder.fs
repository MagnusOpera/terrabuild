
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

    let mutable node2Cluster = Map.empty
    let mutable clusters = Map.empty

    let merge clusterId dependencies clusterHash =
        let mutable cluster = Set.empty
        for depId in dependencies do
            let depNode = graph.Nodes[depId]
            let depClusterId = node2Cluster[depId]
            if clusterHash = depNode.ClusterId && clusterId <> depClusterId then
                for nid in clusters[depClusterId] do
                    node2Cluster <- node2Cluster |> Map.add nid clusterId
                    cluster <- cluster |> Set.add nid
                clusters <- clusters |> Map.remove depClusterId
        cluster

    let rec visit nodeId =
        let node = graph.Nodes[nodeId]
        if not (node2Cluster.ContainsKey nodeId) then
            for depId in node.Dependencies do
                visit depId
            let clusterId = nextClusterId()
            let cluster = merge clusterId node.Dependencies node.ClusterId |> Set.add nodeId
            node2Cluster <- node2Cluster |> Map.add nodeId clusterId
            clusters <- clusters |> Map.add clusterId cluster

    for root in graph.RootNodes do visit root
    let rootHashs = graph.RootNodes |> Set.map (fun rootId -> graph.Nodes[rootId].ClusterId)
    for rootHash in rootHashs do
        let clusterId = nextClusterId()
        let cluster = merge clusterId graph.RootNodes rootHash
        if cluster.Count > 0 then
            clusters <- clusters |> Map.add clusterId cluster

    // remove clusters with 1 node
    for (KeyValue(clusterId, nodeIds)) in clusters do
        if nodeIds.Count <= 1 then
            for nodeId in nodeIds do node2Cluster <- node2Cluster |> Map.remove nodeId
            clusters <- clusters |> Map.remove clusterId

    let nodes =
        graph.Nodes
        |> Map.map (fun nodeId node ->
            match node2Cluster |> Map.tryFind nodeId with
            | Some clusterId -> { node with ClusterId = Some clusterId }
            | _ -> { node with ClusterId = None })

    let graph = { graph with Graph.Nodes = nodes }
    graph
