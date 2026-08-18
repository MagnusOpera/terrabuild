module GraphPipeline.Selection
open Collections
open GraphDef
open Serilog

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

    let rootNodes =
        let allNodeIds = nodes |> Map.keys |> Set.ofSeq
        let allDependencyIds = nodes |> Map.values |> Seq.collect (fun node -> node.Dependencies) |> Set.ofSeq
        allNodeIds - allDependencyIds

    let selectedGraph =
        { Graph.Nodes = nodes
          Graph.RootNodes = rootNodes
          Graph.Batches = Map.empty
          Graph.Phases = graph.Phases }

    selectedGraph.Nodes
    |> Map.values
    |> Seq.iter (fun node ->
        let inputs = environmentSensitiveInputs node.EvaluationInputs
        let status = environmentSensitivityStatus node.EnvironmentSensitive node.EvaluationInputs
        if status = "missing-opt-in" || status = "declared-neutral" then
            let inputNames = inputs |> List.map _.Name |> String.join ", "
            let message = $"Target '{node.Id}' consumes environment-sensitive inputs without environment_sensitive = true: {inputNames}. Its artifacts may not be reusable across environments."
            Log.Warning("{Warning}", message)
            $"{Ansi.Emojis.warning} {message}" |> Terminal.writeLine)

    selectedGraph
