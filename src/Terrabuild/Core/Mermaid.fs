module Mermaid
open GraphDef
open System



type GetStatus = Node -> string

type GetOrigin = Node -> Build.TaskRequest option


let render (getStatus: GetStatus option) (getOrigin: GetOrigin option) (graph: Graph) =
    let mermaid = [
        "flowchart TD"
        $"classDef build stroke:red,stroke-width:3px"
        $"classDef restore stroke:orange,stroke-width:3px"
        $"classDef ignore stroke:black,stroke-width:3px"

        for (KeyValue(_, node)) in graph.Nodes do
            let status =
                getStatus
                |> Option.map (fun getNodeStatus -> getNodeStatus node)
                |> Option.defaultValue ""

            let nodeTitle =
                match node.ProjectName with
                | None -> node.Target
                | Some projectId -> $"{node.Target} {projectId}"
            $"{node.Id}(\"<b>{nodeTitle}</b> {status}\n{node.ProjectDir}\")"

        for (KeyValue(_, node)) in graph.Nodes do
            for dependency in node.Dependencies do
                match graph.Nodes |> Map.tryFind dependency with
                | Some dstNode -> $"{node.Id} --> {dstNode.Id}"
                | _ -> ()

            let origin =
                getOrigin
                |> Option.bind (fun getOrigin -> getOrigin node)

            match origin with
            | Some request when request.IsBuild -> $"class {node.Id} build"
            | Some request when request.IsRestore -> $"class {node.Id} restore"
            | _ -> $"class {node.Id} ignore"
    ]

    mermaid

