module Mermaid
open GraphDef
open System
open System.Text.RegularExpressions



type GetStatus = Node -> string

type GetOrigin = Node -> Runner.TaskRequest option


let render (getStatus: GetStatus option) (getOrigin: GetOrigin option) (graph: Graph) =
    let sanitize (s: string) =
        s.Replace("@", "#")

    let phaseId (phase: string) =
        let normalized = Regex.Replace(phase, "[^A-Za-z0-9_]", "_")
        let suffix = Hash.sha256 phase |> fun hash -> hash.Substring(0, 8)
        $"phase_{normalized}_{suffix}"

    let participatingPhases =
        let rec addDependencies phases phase =
            if phases |> Set.contains phase then phases
            else
                graph.Phases
                |> Map.tryFind phase
                |> Option.defaultValue Set.empty
                |> Set.fold addDependencies (phases |> Set.add phase)

        graph.Nodes
        |> Map.values
        |> Seq.choose _.Phase
        |> Seq.fold addDependencies Set.empty

    let renderNode (node: Node) =
        let status =
            getStatus
            |> Option.map (fun getNodeStatus -> getNodeStatus node)
            |> Option.defaultValue ""

        let nodeTitle =
            match node.ProjectName with
            | None -> node.Target
            | Some projectId -> $"{node.Target} {projectId}"
        $"{node.Id |> sanitize}(\"<b>{nodeTitle}</b> {status}\n{node.ProjectDir}\")"

    let mermaid = [
        "flowchart TD"
        $"classDef build stroke:red,stroke-width:3px"
        $"classDef restore stroke:orange,stroke-width:3px"
        $"classDef ignore stroke:black,stroke-width:3px"

        for node in graph.Nodes.Values |> Seq.filter (fun node -> node.Phase.IsNone) do
            renderNode node

        for phase in participatingPhases do
            let id = phaseId phase
            $"subgraph {id}[\"Phase: {phase}\"]"
            $"{id}_gate{{{{\"{phase}\"}}}}"
            for node in graph.Nodes.Values |> Seq.filter (fun node -> node.Phase = Some phase) do
                renderNode node
            "end"

        for phase in participatingPhases do
            for dependency in graph.Phases |> Map.tryFind phase |> Option.defaultValue Set.empty do
                if participatingPhases |> Set.contains dependency then
                    $"{phaseId phase}_gate -.-> {phaseId dependency}_gate"

        for (KeyValue(_, node)) in graph.Nodes do
            for dependency in node.Dependencies do
                match graph.Nodes |> Map.tryFind dependency with
                | Some dstNode -> $"{node.Id |> sanitize} --> {dstNode.Id |> sanitize}"
                | _ -> ()

            let origin =
                getOrigin
                |> Option.bind (fun getOrigin -> getOrigin node)

            match origin with
            | Some request when request.IsExec -> $"class {node.Id |> sanitize} build"
            | Some request when request.IsRestore -> $"class {node.Id |> sanitize} restore"
            | _ -> $"class {node.Id |> sanitize} ignore"
    ]

    mermaid
