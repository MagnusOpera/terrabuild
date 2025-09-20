
module ClusterBuilder
open GraphDef


type Cluster = {
    Id: string
    Nodes: Set<string>
}

let computeClusters (graph: Graph) =
    let mutable clusterIdCounter = 0
    let nextClusterId () =
        clusterIdCounter <- clusterIdCounter + 1
        $"cluster-{clusterIdCounter}"

    let nodeToCluster = System.Collections.Generic.Dictionary<string, string>()
    let clusters = System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>>()

    let rec visit (nodeId: string) =
        if not (nodeToCluster.ContainsKey nodeId) then
            let node = graph.Nodes[nodeId]

            for depId in node.Dependencies do
                visit depId

            let cid = nextClusterId()
            nodeToCluster[nodeId] <- cid
            clusters[cid] <- System.Collections.Generic.HashSet<string>([ nodeId ])
            for depId in node.Dependencies do
                visit depId

                let depNode = graph.Nodes[depId]
                let cidDep = nodeToCluster[depId]
                if depNode.ClusterHash = node.ClusterHash && cid <> cidDep then
                    for nid in clusters[cidDep] do
                        nodeToCluster[nid] <- cid
                        clusters[cid].Add nid |> ignore
                    clusters.Remove cidDep |> ignore

    for root in graph.RootNodes do
        visit root

    // Build final immutable representation
    let clusters =
        clusters
        |> Seq.map (fun kvp -> { Id = kvp.Key; Nodes = Set.ofSeq kvp.Value })
        |> Seq.toList

    printfn $"All clusters:"
    for cluster in clusters do
        printfn $"Cluster: {cluster.Id}"
        for node in cluster.Nodes do
            printfn $"  {node}"

