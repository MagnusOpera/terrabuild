
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

    let mutable node2cluster = Map.empty
    let mutable clusters = Map.empty

    let merge clusterId dependencies clusterHash =
        let mutable cluster = Set.empty
        for depId in dependencies do
            let depNode = graph.Nodes[depId]
            let depClusterId = node2cluster[depId]
            if clusterHash = depNode.ClusterId && clusterId <> depClusterId then
                for nid in clusters[depClusterId] do
                    node2cluster <- node2cluster |> Map.add nid clusterId
                    cluster <- cluster |> Set.add nid
                clusters <- clusters |> Map.remove depClusterId
        cluster

    let rec visit nodeId =
        let node = graph.Nodes[nodeId]
        if not (node2cluster.ContainsKey nodeId) then
            for depId in node.Dependencies do
                visit depId
            let clusterId = nextClusterId()
            let cluster = merge clusterId node.Dependencies node.ClusterId |> Set.add nodeId
            node2cluster <- node2cluster |> Map.add nodeId clusterId
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
            for nodeId in nodeIds do node2cluster <- node2cluster |> Map.remove nodeId
            clusters <- clusters |> Map.remove clusterId

    node2cluster, clusters


let build (graph: Graph) =
    let node2cluster, clusters = computeClusters graph

    let nodes =
        graph.Nodes
        |> Map.map (fun nodeId node ->
            match node2cluster |> Map.tryFind nodeId with
            | Some clusterId -> { node with ClusterId = Some clusterId }
            | _ -> { node with ClusterId = None })

    let graph = { graph with Graph.Nodes = nodes }








    graph
