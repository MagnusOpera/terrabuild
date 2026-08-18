module GraphPipeline.Topology

open System.Collections.Generic
open Errors
open GraphDef

[<RequireQualifiedAccess>]
type Index = {
    DependencyFirst: string array
    Dependents: Dictionary<string, string array>
}

/// Produce a deterministic topological ordering in which every dependency is
/// positioned before the nodes that depend on it.
let dependencyFirst (graph: Graph) =
    // Match the former recursive resolver's deterministic DFS post-order while
    // avoiding call-stack growth on deep graphs.
    let states = Dictionary<string, byte>(graph.Nodes.Count)
    let dependencyFirst = ResizeArray<string>(graph.Nodes.Count)
    for startId in graph.Nodes.Keys do
        if states.ContainsKey startId |> not then
            let pending = Stack<struct (string * bool)>()
            pending.Push(struct (startId, false))
            while pending.Count > 0 do
                let struct (nodeId, expanded) = pending.Pop()
                if expanded then
                    states[nodeId] <- 2uy
                    dependencyFirst.Add nodeId
                else
                    match states.TryGetValue nodeId with
                    | true, 2uy -> ()
                    | true, _ -> raiseBugError "Cannot index a graph containing a dependency cycle"
                    | _ ->
                        states[nodeId] <- 1uy
                        pending.Push(struct (nodeId, true))
                        for dependencyId in graph.Nodes[nodeId].Dependencies |> Seq.rev do
                            pending.Push(struct (dependencyId, false))

    dependencyFirst.ToArray()

/// Index the immutable graph once for stages that need both traversal directions.
let build (graph: Graph) =
    let dependents = Dictionary<string, ResizeArray<string>>(graph.Nodes.Count)

    for KeyValue(nodeId, node) in graph.Nodes do
        for dependencyId in node.Dependencies do
            match dependents.TryGetValue dependencyId with
            | true, items -> items.Add nodeId
            | _ -> dependents[dependencyId] <- ResizeArray([ nodeId ])

    let frozenDependents = Dictionary<string, string array>(graph.Nodes.Count)
    for nodeId in graph.Nodes.Keys do
        frozenDependents[nodeId] <-
            match dependents.TryGetValue nodeId with
            | true, items -> items |> Seq.sort |> Array.ofSeq
            | _ -> Array.empty

    { Index.DependencyFirst = dependencyFirst graph
      Index.Dependents = frozenDependents }
