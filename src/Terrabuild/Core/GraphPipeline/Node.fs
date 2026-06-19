module GraphPipeline.Node
open Collections
open System.Collections.Generic
open Errors
open Serilog
open System
open GraphDef



// build the high-level graph from configuration
type private VisitState =
    | Visiting
    | Visited

let build (options: ConfigOptions.Options) (configuration: Configuration.Workspace) =
    let startedAt = DateTime.UtcNow
    let allNodes = Dictionary<string, Node>()
    let nodeStates = Dictionary<string, VisitState>()

    // first check all targets exist in WORKSPACE
    match options.Targets |> Seq.tryFind (fun targetName -> configuration.Targets |> Map.containsKey targetName |> not) with
    | Some undefinedTarget -> raiseSymbolError $"Target {undefinedTarget} is not defined in WORKSPACE"
    | _ -> ()


    let buildDependsOn (projectConfig: Configuration.Project) target =
        let buildDependsOn =
            configuration.Targets
            |> Map.tryFind target
            |> Option.defaultValue Set.empty
        let projDependsOn =
            projectConfig.Targets
            |> Map.tryFind target
            |> Option.map (fun ct -> ct.DependsOn)
            |> Option.defaultValue Set.empty
        let dependsOns = buildDependsOn + projDependsOn
        dependsOns


    let buildDependencies (projectConfig: Configuration.Project) (project: string) (target: string) =
        let dependsOns = buildDependsOn projectConfig target

        dependsOns
        |> Seq.collect (fun dependsOn ->
            match dependsOn with
            // Cross-project dependency: ^<target>
            | String.Regex "^\^(.+)$" [ depTarget ] ->
                projectConfig.Dependencies
                |> Seq.choose (fun depProject ->
                    let depConfig = configuration.Projects[depProject]
                    if depConfig.Targets |> Map.containsKey depTarget then
                        Some (depProject, depTarget)
                    else
                        None)

            // Intra-project dependency: <target>
            | depTarget ->
                if projectConfig.Targets |> Map.containsKey depTarget then
                    Seq.singleton (project, depTarget)
                else
                    Seq.empty
        )
        |> Set.ofSeq

    let rec buildNode path project target =
        let projectConfig = configuration.Projects[project]
        let targetConfig = projectConfig.Targets[target]
        let nodeId = $"{project}:{target}"

        let raiseCircularDependency () =
            let chain = path |> List.rev
            let cycle =
                match chain |> List.tryFindIndex ((=) nodeId) with
                | Some index -> (chain |> List.skip index) @ [ nodeId ]
                | None -> chain @ [ nodeId ]
            cycle
            |> String.join " -> "
            |> sprintf "Circular target dependency detected: %s"
            |> raiseInvalidArg

        let processNode() =
            nodeStates[nodeId] <- Visiting

            let allDeps = buildDependencies projectConfig project target
            let children = allDeps |> Set.map (fun (project, target) -> $"{project}:{target}")

            // ensure children exist
            for (project, target) in allDeps do
                buildNode (nodeId :: path) project target

            let targetContent = [
                yield projectConfig.Hash
                yield targetConfig.Hash
                yield! children |> Seq.map (fun nodeId -> allNodes[nodeId].TargetHash)
            ]
            let targetHash = targetContent |> Hash.sha256strings

            let build =
                if options.Force then BuildMode.Always
                else targetConfig.Build |> Option.defaultValue BuildMode.Auto

            let required = build = BuildMode.Always

            let targetOutput =
                if targetConfig.Cache = Some ArtifactMode.None then Set.empty
                else targetConfig.Outputs

            let node =
                { Node.Id = nodeId

                  Node.ProjectId = projectConfig.Id
                  Node.ProjectName = projectConfig.Name
                  Node.ProjectDir = projectConfig.Directory
                  Node.Target = target
                  Node.Operations = []
                  Node.Artifacts = targetConfig.Cache |> Option.defaultValue ArtifactMode.Managed
                  Node.Build = build
                  Node.Batch = targetConfig.Batch
                  Node.Dependencies = children
                  Node.Outputs = targetOutput

                  Node.ClusterHash = None
                  Node.ProjectHash = projectConfig.Hash
                  Node.TargetHash = targetHash
                  Node.Action = RunAction.Ignore
                  Node.Required = required }

            Log.Debug("Node '{NodeId}' has key '{Key}'", nodeId, buildCacheKey node)
            if allNodes.TryAdd(nodeId, node) |> not then raiseBugError "Unexpected graph building race"
            nodeStates[nodeId] <- Visited
  
        match nodeStates.TryGetValue nodeId with
        | true, Visited -> ()
        | true, Visiting -> raiseCircularDependency()
        | _ -> processNode()

    configuration.Projects
    |> Map.iter (fun projectId projectConfig ->
        projectConfig.Targets
        |> Map.keys
        |> Seq.iter (fun target -> buildNode [] projectId target))

    let rootNodes =
        let allNodeIds = allNodes.Keys |> Set
        let allDependencyIds = allNodes.Values |> Seq.collect (fun node -> node.Dependencies) |> Set.ofSeq
        allNodeIds - allDependencyIds

    let endedAt = DateTime.UtcNow
    let buildDuration = endedAt - startedAt
    Log.Debug("Graph Build: {duration}", buildDuration)

    $" {Ansi.Styles.green}{Ansi.Emojis.arrow}{Ansi.Styles.reset} {allNodes.Count} nodes" |> Terminal.writeLine
    $" {Ansi.Styles.green}{Ansi.Emojis.arrow}{Ansi.Styles.reset} {rootNodes.Count} root nodes" |> Terminal.writeLine

    { Graph.Nodes = allNodes |> Map.ofDict
      Graph.RootNodes = rootNodes
      Graph.Batches = Map.empty }
