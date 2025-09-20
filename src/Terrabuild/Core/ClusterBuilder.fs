
module ClusterBuilder

let build (options: ConfigOptions.Options) (graph: GraphDef.Graph) =
    // first get clusters
    let clusters =
        graph.Nodes
        |> Seq.groupBy (fun (KeyValue(_, node)) -> node.Lineage)
        |> Map.ofSeq
        |> Map.map (fun _ v -> v |> List.ofSeq)
        |> Map.filter (fun _ v -> v |> List.length > 1)

    $"Found {clusters.Count} clusters" |> Terminal.writeLine

    graph

