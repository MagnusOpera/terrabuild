module GraphPipeline.Selection
open Collections
open System.Collections.Generic
open GraphDef

let build (options: ConfigOptions.Options) (configuration: Configuration.Workspace) (graph: Graph) =
    let selectedRoots =
        configuration.SelectedProjects
        |> Seq.collect (fun projectId ->
            options.Targets
            |> Seq.choose (fun target ->
                configuration.Projects
                |> Map.tryFind projectId
                |> Option.bind (fun project ->
                    if project.Targets |> Map.containsKey target then Some $"{projectId}:{target}"
                    else None)))
        |> Set.ofSeq

    let activeNodes = HashSet<string>()
    let pending = Stack<string>(selectedRoots)
    while pending.Count > 0 do
        let nodeId = pending.Pop()
        if activeNodes.Add nodeId then
            match graph.Nodes |> Map.tryFind nodeId with
            | Some node ->
                for dependencyId in node.Dependencies do
                    pending.Push dependencyId
            | None -> ()

    let nodes =
        graph.Nodes
        |> Map.filter (fun nodeId _ -> activeNodes.Contains nodeId)

    let rootNodes =
        let allNodeIds = nodes |> Map.keys |> Set.ofSeq
        let allDependencyIds = nodes |> Map.values |> Seq.collect (fun node -> node.Dependencies) |> Set.ofSeq
        allNodeIds - allDependencyIds

    { Graph.Nodes = nodes
      Graph.RootNodes = rootNodes
      Graph.Batches = Map.empty
      Graph.Phases = graph.Phases }
