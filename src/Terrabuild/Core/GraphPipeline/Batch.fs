module GraphPipeline.Batch
open System.Collections.Generic
open Collections
open GraphDef
open Errors

type Batch =
    { BatchId: string
      ClusterHash: string
      Nodes: Node list }

let private computeBatchId (clusterHash: string) (nodes: Node list) =
    let content =
        clusterHash
        :: (nodes |> List.map (fun n -> n.Id) |> List.sort)
    Hash.sha256strings content

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

let computeBatches (graph: Graph) =
    graph.Nodes
    |> Seq.choose (fun (KeyValue(_, node)) ->
        match node.ClusterHash with
        | Some clusterHash -> Some (clusterHash, node)
        | _ -> None)
    |> Seq.groupBy fst
    |> Seq.collect (fun (clusterHash, items) ->
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

            let partitionGroups = batchModes |> Map.tryFind Group.Partition |> Option.defaultValue []  
            let allGroup = batchModes |> Map.tryFind Group.All |> Option.defaultValue []  
        
            partitionGroups
            |> partitionByDependencies
            |> (fun partitionGroups -> allGroup :: partitionGroups)
            |> Seq.choose (fun comp ->
                // only batch if > 1 node and at least one member is actually executing
                if comp.Length <= 1 then None
                else
                    let batchId = computeBatchId clusterHash comp
                    Some { Batch.BatchId = batchId
                           Batch.ClusterHash = clusterHash
                           Batch.Nodes = comp }))
    |> List.ofSeq

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
                nodeIds
                |> List.choose (fun nid -> graph.Nodes |> Map.tryFind nid |> Option.map (fun n -> n.ProjectDir))

            let batchContext =
                Some {
                    Terrabuild.Extensibility.BatchContext.Hash = batch.BatchId
                    Terrabuild.Extensibility.BatchContext.TempDir = options.SharedDir
                    Terrabuild.Extensibility.BatchContext.ProjectPaths = projectDirs
                }

            // reuse the same project/target operations definition as head node
            // NOTE: this assumes batching is only meaningful for nodes with same target (as your previous code)
            let projectId = headNode.ProjectId
            let projectConfig = configuration.Projects[projectId]
            let targetConfig = projectConfig.Targets[headNode.Target]

            let ops =
                targetConfig.Operations
                |> List.collect (fun operation ->
                    let optContext =
                        { Terrabuild.Extensibility.ActionContext.Debug = options.Debug
                          Terrabuild.Extensibility.ActionContext.CI = options.Run.IsSome
                          Terrabuild.Extensibility.ActionContext.Command = operation.Command
                          Terrabuild.Extensibility.ActionContext.Hash = batch.ClusterHash
                          Terrabuild.Extensibility.ActionContext.Batch = batchContext }

                    let parameters =
                        match operation.Context with
                        | Terrabuild.Expressions.Value.Map map ->
                            map
                            |> Map.add "context" (Terrabuild.Expressions.Value.Object optContext)
                            |> Terrabuild.Expressions.Value.Map
                        | _ -> raiseBugError "Failed to get context (internal error)"

                    match Extensions.invokeScriptMethod<Terrabuild.Extensibility.ShellOperations> optContext.Command parameters (Some operation.Script) with
                    | Extensions.InvocationResult.Success executionRequest ->
                        executionRequest |> List.map (fun shellOperation -> {
                            ContaineredShellOperation.Image = operation.Image
                            ContaineredShellOperation.Platform = operation.Platform
                            ContaineredShellOperation.Variables = operation.ContainerVariables
                            ContaineredShellOperation.Envs = operation.Envs
                            ContaineredShellOperation.MetaCommand = $"{operation.Extension} {operation.Command}"
                            ContaineredShellOperation.Command = shellOperation.Command
                            ContaineredShellOperation.Arguments = shellOperation.Arguments |> String.normalizeShellArgs
                            ContaineredShellOperation.ErrorLevel = shellOperation.ErrorLevel })
                    | Extensions.InvocationResult.ErrorTarget ex ->
                        forwardInvalidArg($"{batch.BatchId}: Failed to get shell operation (extension error)", ex)
                    | _ ->
                        raiseInvalidArg $"{batch.BatchId}: Failed to get shell operation (extension error)"
                )

            // Dependencies of the batch node:
            // union of member deps, minus members themselves.
            // NOTE: keep raw ids; runner will map member->batch at schedule time.
            let memberSet = nodeIds |> Set.ofList
            let dependencySet = batch.Nodes |> Seq.collect (fun n -> n.Dependencies) |> Set.ofSeq
            let batchDependencies = dependencySet - memberSet

            let batchNode =
                { GraphDef.Node.Id = batch.BatchId
                  GraphDef.Node.ProjectId = batch.BatchId
                  GraphDef.Node.ProjectName = None
                  GraphDef.Node.ProjectDir = "."
                  GraphDef.Node.Target = headNode.Target
                  GraphDef.Node.Operations = ops
                  GraphDef.Node.Artifacts = headNode.Artifacts
                  GraphDef.Node.Dependencies = batchDependencies
                  GraphDef.Node.Outputs = Set.empty
                  GraphDef.Node.ClusterHash = Some batch.ClusterHash
                  GraphDef.Node.ProjectHash = batch.BatchId
                  GraphDef.Node.TargetHash = headNode.TargetHash
                  GraphDef.Node.Action = NodeAction.Build
                  GraphDef.Node.Build = headNode.Build
                  GraphDef.Node.Batch = headNode.Batch }

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
