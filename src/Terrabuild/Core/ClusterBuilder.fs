
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

    let merge clusterId dependencies clusterHash =
        let mutable cluster = Set.empty
        for depId in dependencies do
            let depNode = graph.Nodes[depId]
            let depClusterId = nodeToCluster[depId]
            if clusterHash = depNode.ClusterHash && clusterId <> depClusterId then
                for nid in clusters[depClusterId] do
                    nodeToCluster <- nodeToCluster |> Map.add nid clusterId
                    cluster <- cluster |> Set.add nid
                clusters <- clusters |> Map.remove depClusterId
        cluster

    let rec visit nodeId =
        let node = graph.Nodes[nodeId]
        if not (nodeToCluster.ContainsKey nodeId) then
            for depId in node.Dependencies do
                visit depId
            let clusterId = nextClusterId()
            let cluster = merge clusterId node.Dependencies node.ClusterHash |> Set.add nodeId
            nodeToCluster <- nodeToCluster |> Map.add nodeId clusterId
            clusters <- clusters |> Map.add clusterId cluster

    for root in graph.RootNodes do visit root
    let rootHashs = graph.RootNodes |> Set.map (fun rootId -> graph.Nodes[rootId].ClusterHash)
    for rootHash in rootHashs do
        let clusterId = nextClusterId()
        let cluster = merge clusterId graph.RootNodes rootHash
        if cluster.Count > 0 then
            clusters <- clusters |> Map.add clusterId cluster

    let graph = 
        { graph with
            Graph.Node2Cluster = nodeToCluster
            Graph.Clusters = clusters }
    graph
