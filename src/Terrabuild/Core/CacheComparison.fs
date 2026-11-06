module CacheComparison

open System
open System.IO
open System.Text
open System.Text.Json
open Errors

type HashChange = {
    Name: string
    Kind: string
    BeforeHash: string option
    AfterHash: string option
}

type NodeChange = {
    Id: string
    Kind: string
    BeforeKey: string option
    AfterKey: string option
    BeforeAction: string option
    AfterAction: string option
    BeforeReason: string option
    AfterReason: string option
    DecisionChanged: bool
    Reasons: string list
    Files: HashChange list
    ProjectDependencies: HashChange list
    TargetDependencies: HashChange list
    EvaluatedInputs: HashChange list
}

type Comparison = {
    SchemaVersion: int
    Counts: Map<string, int>
    Nodes: NodeChange list
}

let private normalizePath (value: string) =
    value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
    |> Array.fold (fun parts segment ->
        match segment, parts with
        | ".", _ -> parts
        | "..", head :: tail when head <> ".." -> tail
        | _ -> segment :: parts) []
    |> List.rev
    |> String.concat "/"

let private nodeKey (node: Diagnostics.NodeReport) =
    $"{(normalizePath node.ProjectDirectory).ToLowerInvariant()}:{node.Target.ToLowerInvariant()}"

let private changes (before: Map<string, string>) (after: Map<string, string>) =
    Set.union (before.Keys |> Set.ofSeq) (after.Keys |> Set.ofSeq)
    |> Seq.choose (fun name ->
        let oldHash, newHash = Map.tryFind name before, Map.tryFind name after
        if oldHash = newHash then None
        else Some {
            HashChange.Name = name
            Kind = if oldHash.IsNone then "added" elif newHash.IsNone then "removed" else "modified"
            BeforeHash = oldHash
            AfterHash = newHash
        })
    |> Seq.toList

let private keyedNodes (report: Diagnostics.Report) =
    let grouped = report.Nodes |> List.groupBy nodeKey
    match grouped |> List.tryFind (fun (_, nodes) -> nodes.Length > 1) with
    | Some (key, _) -> raiseInvalidArg $"Diagnostic report contains duplicate target identity '{key}'."
    | None -> grouped |> List.map (fun (key, nodes) -> key, nodes.Head) |> Map.ofList

let compare (before: Diagnostics.Report) (after: Diagnostics.Report) =
    if before.SchemaVersion <> 6 || after.SchemaVersion <> 6 then
        raiseInvalidArg "Cache comparison requires diagnostic schema version 6. Capture both runs with a compatible Terrabuild --debug."
    let oldNodes, newNodes = keyedNodes before, keyedNodes after
    let oldProjects = before.Projects |> List.map (fun project -> project.Id, project) |> Map.ofList
    let newProjects = after.Projects |> List.map (fun project -> project.Id, project) |> Map.ofList
    let projectFor projects (node: Diagnostics.NodeReport option) =
        node |> Option.bind (fun node -> Map.tryFind node.ProjectId projects)
    let projectFiles project =
        project |> Option.map (fun (project: Diagnostics.ProjectFingerprint) ->
            project.Files |> List.map (fun file -> normalizePath file.Path, file.Hash) |> Map.ofList) |> Option.defaultValue Map.empty
    let projectDependencies project =
        project |> Option.map (fun (project: Diagnostics.ProjectFingerprint) ->
            project.Dependencies |> List.map (fun dependency -> dependency.Id, dependency.Hash) |> Map.ofList) |> Option.defaultValue Map.empty
    let targetDependencies (node: Diagnostics.NodeReport option) =
        node |> Option.bind _.Fingerprint |> Option.map (fun fingerprint ->
            fingerprint.DependencyTargetHashes |> List.map (fun dependency -> dependency.Id, dependency.Hash) |> Map.ofList) |> Option.defaultValue Map.empty
    let inputs (node: Diagnostics.NodeReport option) =
        node |> Option.map (fun node -> node.EvaluationInputs |> List.map (fun input -> input.Name, input.ValueHash) |> Map.ofList) |> Option.defaultValue Map.empty
    let cacheKey (node: Diagnostics.NodeReport option) = node |> Option.bind _.Fingerprint |> Option.map _.CacheKey
    let action (node: Diagnostics.NodeReport option) = node |> Option.bind _.Action
    let nodes =
        Set.union (oldNodes.Keys |> Set.ofSeq) (newNodes.Keys |> Set.ofSeq)
        |> Seq.choose (fun key ->
            let oldNode, newNode = Map.tryFind key oldNodes, Map.tryFind key newNodes
            if not (oldNode |> Option.exists _.Selected) && not (newNode |> Option.exists _.Selected) then None
            else
                let oldKey, newKey = cacheKey oldNode, cacheKey newNode
                let oldProject, newProject = projectFor oldProjects oldNode, projectFor newProjects newNode
                let files = changes (projectFiles oldProject) (projectFiles newProject)
                let projectDeps = changes (projectDependencies oldProject) (projectDependencies newProject)
                let targetDeps = changes (targetDependencies oldNode) (targetDependencies newNode)
                let evaluated = changes (inputs oldNode) (inputs newNode)
                let kind =
                    if (oldNode.IsNone && before.Run.Completeness <> "complete") ||
                       (newNode.IsNone && after.Run.Completeness <> "complete") then "unresolved"
                    elif oldNode.IsNone then "added"
                    elif newNode.IsNone then "removed"
                    elif oldKey.IsNone || newKey.IsNone then "unresolved"
                    elif oldKey = newKey then "unchanged"
                    else "changed"
                let fingerprints = oldNode |> Option.bind _.Fingerprint, newNode |> Option.bind _.Fingerprint
                let reasons = [
                    if files <> [] then "project-files-changed"
                    if projectDeps <> [] then "project-dependencies-changed"
                    if targetDeps <> [] then "target-dependencies-changed"
                    if evaluated <> [] then "evaluated-inputs-changed"
                    match fingerprints with
                    | Some old, Some current ->
                        if old.DeclaredTargetHash <> current.DeclaredTargetHash then "target-definition-changed"
                        if old.OperationHashes <> current.OperationHashes then "operations-changed"
                    | _ -> ()
                    if (oldNode |> Option.map _.Selected) <> (newNode |> Option.map _.Selected) then "selection-changed"
                ]
                let reasons =
                    if kind = "changed" && reasons.IsEmpty then [ "unattributed-fingerprint-change" ]
                    elif kind = "unresolved" && (oldNode.IsNone || newNode.IsNone) then [ "incomplete-report" ]
                    elif kind = "unresolved" then "fingerprint-unavailable" :: reasons
                    else reasons
                let decisionChanged =
                    (oldNode |> Option.map (fun node -> node.Action, node.ActionReason, node.Cache |> Option.map _.Lookup)) <>
                    (newNode |> Option.map (fun node -> node.Action, node.ActionReason, node.Cache |> Option.map _.Lookup))
                Some {
                    NodeChange.Id = key
                    Kind = kind
                    BeforeKey = oldKey
                    AfterKey = newKey
                    BeforeAction = action oldNode
                    AfterAction = action newNode
                    BeforeReason = oldNode |> Option.bind _.ActionReason
                    AfterReason = newNode |> Option.bind _.ActionReason
                    DecisionChanged = decisionChanged
                    Reasons = reasons
                    Files = if kind = "unresolved" && (oldNode.IsNone || newNode.IsNone) then [] else files
                    ProjectDependencies = if kind = "unresolved" && (oldNode.IsNone || newNode.IsNone) then [] else projectDeps
                    TargetDependencies = if kind = "unresolved" && (oldNode.IsNone || newNode.IsNone) then [] else targetDeps
                    EvaluatedInputs = if kind = "unresolved" && (oldNode.IsNone || newNode.IsNone) then [] else evaluated
                }) |> Seq.toList
    { Comparison.SchemaVersion = 1
      Counts = nodes |> List.countBy _.Kind |> Map.ofList
      Nodes = nodes }

let readReport path =
    try
        let contents = File.ReadAllText(path)
        use document = JsonDocument.Parse(contents)
        let root = document.RootElement
        if root.GetProperty("schemaVersion").GetInt32() <> 6 then
            raiseInvalidArg "Cache comparison requires diagnostic schema version 6."
        if root.GetProperty("nodes").ValueKind <> JsonValueKind.Array || root.GetProperty("projects").ValueKind <> JsonValueKind.Array then
            raiseInvalidArg "Cache comparison requires nodes and projects arrays."
        let report = contents |> Json.Deserialize<Diagnostics.Report>
        if report.Nodes |> List.exists (fun node -> String.IsNullOrWhiteSpace(node.ProjectDirectory) || String.IsNullOrWhiteSpace(node.Target)) then
            raiseInvalidArg "Diagnostic nodes require project directories and target names."
        report
    with
    | :? TerrabuildException -> reraise()
    | _ -> raiseInvalidArg $"Cannot read diagnostic report '{path}'. Expected a Terrabuild --debug JSON report."

let render (comparison: Comparison) =
    let output = StringBuilder()
    output.AppendLine("Cache key comparison") |> ignore
    for kind in [ "changed"; "added"; "removed"; "unresolved"; "unchanged" ] do
        output.AppendLine($"  {kind}: {comparison.Counts |> Map.tryFind kind |> Option.defaultValue 0}") |> ignore
    for node in comparison.Nodes do
        if node.Kind <> "unchanged" || node.DecisionChanged then
            output.AppendLine($"{node.Id}: {node.Kind}") |> ignore
            if node.DecisionChanged then
                let beforeAction = node.BeforeAction |> Option.defaultValue "unresolved"
                let afterAction = node.AfterAction |> Option.defaultValue "unresolved"
                let beforeReason = node.BeforeReason |> Option.defaultValue "unknown"
                let afterReason = node.AfterReason |> Option.defaultValue "unknown"
                output.AppendLine($"  cache decision: {beforeAction} ({beforeReason}) -> {afterAction} ({afterReason})") |> ignore
                if node.Kind = "unchanged" then
                    output.AppendLine("  fingerprint unchanged; cache availability or execution policy changed") |> ignore
            for reason in node.Reasons do output.AppendLine($"  {reason}") |> ignore
            for label, changes in [ "file", node.Files; "project dependency", node.ProjectDependencies; "target dependency", node.TargetDependencies; "input", node.EvaluatedInputs ] do
                for change in changes do output.AppendLine($"  {label} {change.Kind}: {change.Name}") |> ignore
    output.ToString().TrimEnd()
