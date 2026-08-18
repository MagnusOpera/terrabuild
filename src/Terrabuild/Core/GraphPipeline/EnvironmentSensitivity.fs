module GraphPipeline.EnvironmentSensitivity

open Collections
open Errors
open GraphDef

[<RequireQualifiedAccess>]
type Violation = {
    NodeId: string
    Inputs: string list
}

let findViolations (graph: Graph) =
    graph.Nodes
    |> Map.values
    |> Seq.choose (fun node ->
        let inputs = environmentSensitiveInputs node.EvaluationInputs
        if inputs <> [] && node.EnvironmentSensitive <> Some true then
            Some {
                Violation.NodeId = node.Id
                Violation.Inputs = inputs |> List.map _.Name
            }
        else
            None)
    |> Seq.sortBy _.NodeId
    |> List.ofSeq

let validate (graph: Graph) =
    match findViolations graph with
    | [] -> graph
    | violations ->
        let details =
            violations
            |> List.map (fun violation ->
                let inputNames = violation.Inputs |> String.join ", "
                $"- {violation.NodeId}: {inputNames}")
            |> String.join "\n"

        raiseInvalidArg
            $"Environment-neutral targets consume environment-sensitive inputs:\n{details}\nSet environment_sensitive = true only on targets that intentionally produce environment-specific artifacts.\nRun 'terrabuild explain <target>' with the same options to inspect decisions and evaluated inputs."
