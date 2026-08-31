module GraphPipeline.Batch
open System.Collections.Generic
open Collections
open GraphDef
open Errors
open Serilog
open Terrabuild.Expression

type Batch =
    { BatchId: string
      ClusterHash: string
      Phase: string option
      Nodes: Node list }

let private computeBatchId (clusterHash: string) (nodes: Node list) =
    let content =
        clusterHash
        :: (nodes |> List.map (fun n -> n.Id) |> List.sort)
    Hash.sha256strings content

let internal mergeBatchEnvironments
    target
    step
    (sources: (string * Map<string, string>) list) =
    let merged =
        sources
        |> List.sortBy fst
        |> List.fold (fun mergedByName (source, environments) ->
            environments
            |> Map.fold (fun mergedByName name value ->
                match mergedByName |> Map.tryFind name with
                | None ->
                    mergedByName |> Map.add name (value, [ source ])
                | Some (existingValue, existingSources) when existingValue = value ->
                    mergedByName |> Map.add name (existingValue, source :: existingSources)
                | Some (_, existingSources) ->
                    let conflictingSources =
                        source :: existingSources
                        |> List.distinct
                        |> List.sort
                        |> List.map (sprintf "'%s'")
                        |> String.concat ", "

                    raiseInvalidArg
                        $"Cannot batch target '{target}' step '{step}': environment variable '{name}' has conflicting values for {conflictingSources}. Configure batch = ~never for targets that require project-specific values."
            ) mergedByName
        ) Map.empty

    merged
    |> Map.map (fun _ (value, _) -> value)

let internal mergeBatchVariables (sources: Set<string> list) =
    sources
    |> List.fold Set.union Set.empty

let internal mergeBatchContexts
    target
    step
    (sources: (string * Value) list) =
    let merged =
        sources
        |> List.sortBy fst
        |> List.fold (fun mergedByName (source, context) ->
            let entries =
                match context with
                | Value.Map entries -> entries
                | _ ->
                    raiseBugError
                        $"Cannot batch target '{target}' step '{step}': '{source}' has a non-map action context."

            entries
            |> Map.fold (fun mergedByName name value ->
                match mergedByName |> Map.tryFind name with
                | None ->
                    mergedByName |> Map.add name (value, [ source ])
                | Some (existingValue, existingSources) when existingValue = value ->
                    mergedByName |> Map.add name (existingValue, source :: existingSources)
                | Some (_, existingSources) ->
                    let conflictingSources =
                        source :: existingSources
                        |> List.distinct
                        |> List.sort
                        |> List.map (sprintf "'%s'")
                        |> String.concat ", "

                    raiseInvalidArg
                        $"Cannot batch target '{target}' step '{step}': action argument '{name}' has conflicting values for {conflictingSources}. Configure batch = ~never for targets that require project-specific values."
            ) mergedByName
        ) Map.empty

    merged
    |> Map.map (fun _ (value, _) -> value)
    |> Value.Map

let private mergeBatchTargetSteps
    (configuration: Configuration.Workspace)
    (batch: Batch)
    (headTarget: Configuration.Target) =
    let memberSteps =
        batch.Nodes
        |> List.sortBy _.Id
        |> List.map (fun node ->
            let project = configuration.Projects[node.ProjectId]
            let target = project.Targets[node.Target]
            node.Id, target.Steps)

    memberSteps
    |> List.iter (fun (nodeId, steps) ->
        if steps.Length <> headTarget.Steps.Length then
            raiseBugError
                $"Cannot batch target '{batch.Nodes.Head.Target}': '{nodeId}' has {steps.Length} steps while the batch head has {headTarget.Steps.Length} steps.")

    headTarget.Steps
    |> List.mapi (fun index headStep ->
        let steps =
            memberSteps
            |> List.map (fun (nodeId, steps) ->
                nodeId, steps |> List.item index)

        let environmentSources =
            steps
            |> List.map (fun (nodeId, step) -> nodeId, step.Envs)

        let variableSources =
            steps
            |> List.map (fun (_, step) -> step.ContainerVariables)

        let contextSources =
            steps
            |> List.map (fun (nodeId, step) -> nodeId, step.Context)

        let step = $"{headStep.Extension} {headStep.Command}"
        let environments =
            mergeBatchEnvironments batch.Nodes.Head.Target step environmentSources
        let variables = mergeBatchVariables variableSources
        let context =
            mergeBatchContexts batch.Nodes.Head.Target step contextSources

        { headStep with
            ContainerVariables = variables
            Envs = environments
            Context = context })

let internal computeBatchTargetHash (batch: Batch) (operations: ContaineredShellOperation list) =
    [ yield batch.ClusterHash
      yield!
          batch.Nodes
          |> List.sortBy _.Id
          |> List.collect (fun node -> [ node.Id; node.TargetHash ])
      yield! operations |> List.map Json.Serialize ]
    |> Hash.sha256strings

let private partitionByDependencies (bucketNodes: Node list) =
    // Undirected connectivity inside the bucket:
    // edge A—B if A depends on B or B depends on A (restricted to bucket)
    let ids = bucketNodes |> List.map (fun n -> n.Id) |> Set.ofList
    let nodesById = bucketNodes |> List.map (fun n -> n.Id, n) |> Map.ofList

    // Precompute reverse edges inside the bucket for speed/clarity
    let reverseDeps =
        let dict = Dictionary<string, ResizeArray<string>>()
        for n in bucketNodes do
            for d in n.Dependencies do
                if ids |> Set.contains d then
                    match dict.TryGetValue d with
                    | true, arr -> arr.Add n.Id
                    | _ -> dict[d] <- ResizeArray([ n.Id ])
        dict

    let neighbors (id: string) =
        let n = nodesById[id]
        let depsInBucket = n.Dependencies |> Set.filter (fun d -> ids |> Set.contains d)
        let revInBucket =
            match reverseDeps.TryGetValue id with
            | true, arr -> arr :> seq<string> |> Set.ofSeq
            | _ -> Set.empty
        depsInBucket + revInBucket

    let visited = HashSet<string>()
    let components = ResizeArray<Node list>()

    for id in ids do
        if visited.Add id then
            let stack = Stack<string>()
            stack.Push id
            let compIds = ResizeArray<string>()
            compIds.Add id

            while stack.Count > 0 do
                let cur = stack.Pop()
                for nb in neighbors cur do
                    if visited.Add nb then
                        stack.Push nb
                        compIds.Add nb

            let compNodes =
                compIds
                |> Seq.map (fun cid -> nodesById[cid])
                |> List.ofSeq

            components.Add compNodes

    components |> List.ofSeq

let private hasExternalDependencyCycle (graph: Graph) (candidateNodes: Node list) =
    let memberIds = HashSet<string>(candidateNodes |> Seq.map _.Id)
    let externalDependencies =
        candidateNodes
        |> Seq.collect (fun node -> node.Dependencies)
        |> Set.ofSeq
        |> Set.filter (memberIds.Contains >> not)

    let visited = HashSet<string>()
    let pending = Stack<string>(externalDependencies)
    let mutable reachesMember = false
    while not reachesMember && pending.Count > 0 do
        let nodeId = pending.Pop()
        if memberIds.Contains nodeId then
            reachesMember <- true
        elif visited.Add nodeId then
            match graph.Nodes |> Map.tryFind nodeId with
            | Some node ->
                for dependencyId in node.Dependencies do
                    pending.Push dependencyId
            | None -> ()
    reachesMember

let private contractedDependencies (graph: Graph) (batches: Batch list) =
    let memberToBatch =
        batches
        |> Seq.collect (fun batch ->
            batch.Nodes |> Seq.map (fun node -> node.Id, batch.BatchId))
        |> Map.ofSeq

    let execId nodeId =
        memberToBatch |> Map.tryFind nodeId |> Option.defaultValue nodeId

    let scheduledNodeIds =
        graph.Nodes
        |> Seq.choose (fun (KeyValue(nodeId, node)) ->
            if node.Required || graph.RootNodes.Contains nodeId then Some nodeId
            else None)
        |> Set.ofSeq

    let executionIds =
        scheduledNodeIds
        |> Set.map execId

    let emptyDependencies =
        executionIds
        |> Seq.map (fun nodeId -> nodeId, Set.empty)
        |> Map.ofSeq

    scheduledNodeIds
    |> Seq.fold (fun dependencies nodeId ->
        let sourceId = execId nodeId
        let targetIds =
            graph.Nodes[nodeId].Dependencies
            |> Set.filter (fun dependencyId -> graph.Nodes[dependencyId].Required)
            |> Set.map execId
            |> Set.remove sourceId

        dependencies
        |> Map.change sourceId (fun existing ->
            Some (targetIds + (existing |> Option.defaultValue Set.empty)))) emptyDependencies

let private stronglyConnectedComponents (dependencies: Map<string, Set<string>>) =
    let nodeIds =
        dependencies
        |> Seq.collect (fun (KeyValue(nodeId, targets)) -> Seq.append (Seq.singleton nodeId) targets)
        |> Set.ofSeq

    let visited = HashSet<string>()
    let completed = ResizeArray<string>()

    for startId in nodeIds do
        if visited.Contains startId |> not then
            let pending = Stack<struct (string * bool)>()
            pending.Push(struct (startId, false))

            while pending.Count > 0 do
                let struct (nodeId, expanded) = pending.Pop()
                if expanded then
                    completed.Add nodeId
                elif visited.Add nodeId then
                    pending.Push(struct (nodeId, true))
                    let targets = dependencies |> Map.tryFind nodeId |> Option.defaultValue Set.empty
                    for targetId in targets |> Seq.rev do
                        if visited.Contains targetId |> not then
                            pending.Push(struct (targetId, false))

    let reverseDependencies =
        nodeIds
        |> Seq.map (fun nodeId -> nodeId, ResizeArray<string>())
        |> Map.ofSeq

    for KeyValue(nodeId, targets) in dependencies do
        for targetId in targets do
            reverseDependencies[targetId].Add nodeId

    let assigned = HashSet<string>()
    let components = ResizeArray<Set<string>>()

    for startId in completed |> Seq.rev do
        if assigned.Add startId then
            let group = HashSet<string>()
            let pending = Stack<string>()
            pending.Push startId

            while pending.Count > 0 do
                let nodeId = pending.Pop()
                group.Add nodeId |> ignore
                for sourceId in reverseDependencies[nodeId] do
                    if assigned.Add sourceId then
                        pending.Push sourceId

            components.Add(group |> Set.ofSeq)

    components |> Seq.filter (fun group -> group.Count > 1) |> List.ofSeq

let private removeCyclicBatches (graph: Graph) (batches: Batch list) =
    let rec remove batches =
        let batchIds = batches |> Seq.map _.BatchId |> Set.ofSeq
        let cyclicBatchIds =
            contractedDependencies graph batches
            |> stronglyConnectedComponents
            |> Seq.collect (Set.intersect batchIds)
            |> Set.ofSeq

        if cyclicBatchIds.IsEmpty then
            batches
        else
            for batchId in cyclicBatchIds do
                Log.Debug(
                    "Skipping batch '{BatchId}' because the contracted execution graph contains a cycle",
                    batchId)

            batches
            |> List.filter (fun batch -> cyclicBatchIds.Contains batch.BatchId |> not)
            |> remove

    remove batches

let computeBatches (graph: Graph) =
    // find clusters with at least one exec node
    let eligibleBuckets =
        graph.Nodes
        |> Seq.choose (fun (KeyValue(_, node)) -> 
            match node with
            | { Action = RunAction.Exec; ClusterHash = Some clusterHash } -> Some (clusterHash, node.Phase)
            | _ -> None)
        |> Set.ofSeq

    graph.Nodes
    |> Seq.choose (fun (KeyValue(_, node)) ->
        match node with
        | { ClusterHash = Some clusterHash; Required = true } when eligibleBuckets |> Set.contains (clusterHash, node.Phase) ->
            Some ((clusterHash, node.Phase), node)
        | _ -> None)
    |> Seq.groupBy fst
    |> Seq.collect (fun ((clusterHash, phase), items) ->
        let bucketNodes =
            items
            |> Seq.map snd
            |> Seq.filter (fun n -> n.ClusterHash.IsSome) // only batch-eligible
            |> List.ofSeq

        // if fewer than 2, no possible batch
        if bucketNodes.Length <= 1 then Seq.empty
        else
            let batchModes = 
                bucketNodes
                |> List.groupBy (fun node -> node.Batch)
                |> Map.ofSeq

            let partitionGroups = batchModes |> Map.tryFind BatchMode.Partition |> Option.defaultValue []  
            let allGroup = batchModes |> Map.tryFind BatchMode.Single |> Option.defaultValue []  
        
            partitionGroups
            |> partitionByDependencies
            |> (fun partitionGroups -> allGroup :: partitionGroups)
            |> Seq.choose (fun comp ->
                // only batch if > 1 node and at least one member is actually executing
                if comp.Length <= 1 then None
                elif comp |> List.exists (fun node -> node.Action = RunAction.Exec) |> not then None
                elif hasExternalDependencyCycle graph comp then
                    Log.Debug(
                        "Skipping batch candidate in cluster '{ClusterHash}' because external dependencies would create a cycle",
                        clusterHash)
                    None
                else
                    let batchId = computeBatchId clusterHash comp
                    Some { Batch.BatchId = batchId
                           Batch.ClusterHash = clusterHash
                           Batch.Phase = phase
                           Batch.Nodes = comp }))
    |> List.ofSeq
    |> removeCyclicBatches graph

let private createBatchNodes (options: ConfigOptions.Options) (configuration: Configuration.Workspace) (graph: GraphDef.Graph) (components: Batch list) =
    components
    |> List.choose (fun batch ->
        let nodeIds = batch.Nodes |> List.map (fun n -> n.Id)
        match nodeIds with
        | [] | [_] -> None
        | headNodeId :: _ ->
            let headNode = graph.Nodes[headNodeId]

            // collect project dirs for BatchContext
            let projectDirs =
                batch.Nodes
                |> List.map _.ProjectDir
                |> Set.ofList

            // reuse the same project/target operations definition as head node
            // NOTE: this assumes batching is only meaningful for nodes with same target (as your previous code)
            let projectId = headNode.ProjectId
            let projectConfig = configuration.Projects[projectId]
            let targetConfig = projectConfig.Targets[headNode.Target]
            let mergedTargetConfig =
                { targetConfig with
                    Steps = mergeBatchTargetSteps configuration batch targetConfig }
            let batchCommands =
                targetConfig.Steps
                |> List.map (fun step -> step.Command)
                |> List.distinct

            let batchContext =
                Some {
                    Terrabuild.ScriptingContracts.BatchContext.Hash = batch.BatchId
                    Terrabuild.ScriptingContracts.BatchContext.TempDir = options.SharedDir
                    Terrabuild.ScriptingContracts.BatchContext.ProjectPaths = projectDirs
                    Terrabuild.ScriptingContracts.BatchContext.BatchCommands = batchCommands
                }

            let _, _, ops =
                Resolve.resolveTargetOperations options projectConfig mergedTargetConfig batch.ClusterHash batchContext

            let batchTargetHash = computeBatchTargetHash batch ops

            // Dependencies of the batch node:
            // union of member deps, minus members themselves.
            // NOTE: keep raw ids; runner will map member->batch at schedule time.
            let memberSet = nodeIds |> Set.ofList
            let dependencySet = batch.Nodes |> Seq.collect (fun n -> n.Dependencies) |> Set.ofSeq
            let batchDependencies = dependencySet - memberSet
            let phaseDependencySet = batch.Nodes |> Seq.collect (fun n -> n.PhaseDependencies) |> Set.ofSeq
            let batchPhaseDependencies = phaseDependencySet - memberSet

            let batchNode =
                { GraphDef.Node.Id = batch.BatchId
                  GraphDef.Node.ProjectId = batch.BatchId
                  GraphDef.Node.ProjectName = None
                  GraphDef.Node.ProjectDir = "."
                  GraphDef.Node.Target = headNode.Target
                  GraphDef.Node.Phase = batch.Phase
                  GraphDef.Node.Locks = batch.Nodes |> Seq.collect _.Locks |> Set.ofSeq
                  GraphDef.Node.Operations = ops
                  GraphDef.Node.EvaluationInputs =
                    batch.Nodes
                    |> Seq.collect _.EvaluationInputs
                    |> Seq.distinctBy (fun input -> input.Name, input.ValueHash)
                    |> Seq.sortBy _.Name
                    |> List.ofSeq
                  GraphDef.Node.EnvironmentSensitive =
                    if batch.Nodes |> List.exists (fun node -> node.EnvironmentSensitive = Some true) then
                        Some true
                    elif batch.Nodes |> List.forall (fun node -> node.EnvironmentSensitive = Some false) then
                        Some false
                    else
                        None
                  GraphDef.Node.Artifacts = headNode.Artifacts
                  GraphDef.Node.Dependencies = batchDependencies
                  GraphDef.Node.PhaseDependencies = batchPhaseDependencies
                  GraphDef.Node.Outputs = Set.empty
                  GraphDef.Node.ClusterHash = Some batch.ClusterHash
                  GraphDef.Node.ProjectHash = batch.BatchId
                  GraphDef.Node.TargetHash = batchTargetHash
                  GraphDef.Node.Action = RunAction.Exec
                  GraphDef.Node.Build = headNode.Build
                  GraphDef.Node.Batch = headNode.Batch
                  GraphDef.Node.Required = true }
            Some (batch.BatchId, batchNode)
    )
    |> Map.ofList

let build (options: ConfigOptions.Options) (configuration: Configuration.Workspace) (graph: GraphDef.Graph) =
    let components = computeBatches graph

    // Graph.Batches: BatchId -> member ids
    let batches =
        components
        |> Seq.map (fun c -> c.BatchId, (c.Nodes |> Seq.map (fun n -> n.Id) |> Set.ofSeq))
        |> Map.ofSeq

    let batchNodes = createBatchNodes options configuration graph components

    // Add batch nodes to the graph; keep original nodes intact for logging
    { graph with
        GraphDef.Graph.Batches = batches
        GraphDef.Graph.Nodes = graph.Nodes |> Map.addMap batchNodes }
    |> Cascade.build
