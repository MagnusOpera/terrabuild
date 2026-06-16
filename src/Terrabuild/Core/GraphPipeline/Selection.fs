module GraphPipeline.Selection
open Collections
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

    let rec visit pending visited =
        match pending with
        | [] -> visited
        | nodeId::rest when visited |> Set.contains nodeId -> visit rest visited
        | nodeId::rest ->
            match graph.Nodes |> Map.tryFind nodeId with
            | Some node ->
                let next = node.Dependencies |> Set.toList
                visit (rest @ next) (visited |> Set.add nodeId)
            | None ->
                visit rest visited

    let activeNodes = visit (selectedRoots |> Set.toList) Set.empty
    let nodes =
        graph.Nodes
        |> Map.filter (fun nodeId _ -> activeNodes |> Set.contains nodeId)

    { Graph.Nodes = nodes
      Graph.RootNodes = selectedRoots |> Set.filter (fun nodeId -> nodes |> Map.containsKey nodeId)
      Graph.Batches = Map.empty }
