module Logs
open Cache
open System
open Humanizer

module Iconography =
    let restore_ok = Ansi.Emojis.popcorn
    let restore_ko = Ansi.Emojis.pretzel
    let build_ok = Ansi.Emojis.green_checkmark
    let build_ko = Ansi.Emojis.red_cross
    let task_status = Ansi.Emojis.eyes
    let task_pending = Ansi.Emojis.construction


let dumpLogs (logId: Guid) (options: ConfigOptions.Options) (cache: ICache) (graph: GraphDef.Graph) (summary: Runner.Summary) =

    let stableRandomId (id: string) =
        $"{logId} {id}" |> Hash.md5 |> String.toLower

    let memberToBatch =
        graph.Batches
        |> Seq.collect (fun (KeyValue(batchId, members)) ->
            members |> Seq.map (fun nodeId -> nodeId, batchId))
        |> Map.ofSeq

    let reportId (nodeId: string) =
        memberToBatch |> Map.tryFind nodeId |> Option.defaultValue nodeId

    let isBatchReport (id: string) =
        graph.Batches |> Map.containsKey id

    let sortedNodes =
        summary.Nodes
        |> Seq.filter (fun (KeyValue(nodeId, _)) ->
            if graph.Batches |> Map.containsKey nodeId then false
            else summary.Nodes |> Map.containsKey nodeId)
        |> Seq.map (fun (KeyValue(nodeId, _)) -> graph.Nodes[nodeId])
        |> Seq.sortBy (fun node ->
            match summary.Nodes |> Map.tryFind node.Id with
            | Some nodeInfo ->
                match nodeInfo.Status with
                | Runner.TaskStatus.Success completionDate -> completionDate
                | Runner.TaskStatus.Failure (completionDate, _) -> completionDate
            | _ -> DateTime.MaxValue)
        |> List.ofSeq


    let dumpMarkdown filename (nodes: GraphDef.Node seq) =
        let nodes = nodes |> List.ofSeq
        let originSummaries =
            nodes
            |> Seq.map (fun node ->
                let cacheEntryId = GraphDef.buildCacheKey node
                node.Id, cache.TryGetSummaryOnly false cacheEntryId)
            |> Map.ofSeq

        let reportGroups =
            nodes
            |> List.groupBy (fun node -> reportId node.Id)
            |> List.map (fun (id, groupNodes) ->
                let representative =
                    let sorted = groupNodes |> List.sortBy (fun n -> n.Id)
                    sorted
                    |> List.tryFind (fun node ->
                        match originSummaries |> Map.tryFind node.Id with
                        | Some (Some _) -> true
                        | _ -> false)
                    |> Option.defaultValue sorted.Head
                id, groupNodes, representative)

        let successful = summary.IsSuccess
        let appendLines lines = IO.appendLinesFile filename lines 
        let append line = appendLines [line]

        let statusEmoji (node: GraphDef.Node) =
            match summary.Nodes |> Map.tryFind node.Id with
            | Some nodeInfo ->
                match nodeInfo.Request, nodeInfo.Status with
                | Runner.TaskRequest.Restore, Runner.TaskStatus.Success _ -> Iconography.restore_ok
                | Runner.TaskRequest.Restore, Runner.TaskStatus.Failure _ -> Iconography.restore_ko
                | Runner.TaskRequest.Exec, Runner.TaskStatus.Success _ -> Iconography.build_ok
                | Runner.TaskRequest.Exec, Runner.TaskStatus.Failure _ -> Iconography.build_ko
            | _ -> Iconography.task_status

        let dumpMarkdown (reportId: string, groupedNodes: GraphDef.Node list, representative: GraphDef.Node) =
            let header =
                let statusEmoji = statusEmoji representative
                let uniqueId = stableRandomId reportId
                let label =
                    if isBatchReport reportId then $"{representative.Target} [batch:{reportId}]"
                    else $"{representative.Target} {representative.ProjectDir}"
                $"## <a name=\"user-content-{uniqueId}\"></a> {statusEmoji} {label}"

            let dumpLogs =
                let originSummary = originSummaries[representative.Id]
                match originSummary with
                | Some (_, summary) -> 
                    let dumpLogs () =
                        summary.Operations |> List.iter (fun group ->
                            group |> List.iter (fun step ->
                                $"### {step.MetaCommand} (exit code {step.ExitCode} - {step.EndedAt})" |> append
                                if options.Debug then
                                    let cmd = $"{step.Command} {step.Arguments}" |> String.trim
                                    $"*{cmd}*" |> append

                                append "```"
                                step.Log |> IO.readTextFile |> append
                                append "```"
                            )
                        )
                    dumpLogs
                | _ ->
                    let dumpNoLog() = $"**No logs available**" |> append
                    dumpNoLog

            header |> append
            if isBatchReport reportId then
                let members = groupedNodes |> List.map (fun node -> node.ProjectDir) |> List.sort |> String.join ", "
                $"*Members:* {members}" |> append
            dumpLogs ()

        let targets = options.Targets |> String.join " "
        let message, color =
            if successful then "success", "success"
            else "failure", "critical"
        let targetsBadge = options.Targets |> String.join "_"
        let summaryAnchor = stableRandomId "summary"
        $"[![{targets}](https://img.shields.io/badge/{targetsBadge}-build_{message}-{color})](#user-content-{summaryAnchor})" |> append

        $"<details><summary>Expand for details</summary>" |> append

        "" |> append
        $"# <a name=\"user-content-{summaryAnchor}\"></a> Summary" |> append
        "| Target | Duration |" |> append
        "|--------|---------:|" |> append

        reportGroups
        |> Seq.map (fun (id, _, representative) ->
            let originSummary = originSummaries[representative.Id]
            let duration =
                match originSummary with
                | Some (_, summary) -> summary.Duration
                | _ -> TimeSpan.Zero

            let label =
                if isBatchReport id then $"{representative.Target} [batch:{id}]"
                else $"{representative.Target} {representative.ProjectDir}"
            id, representative, label, duration)
        |> Seq.sortByDescending (fun (_, _, _, duration) -> duration)
        |> Seq.iter (fun (id, representative, label, duration) ->
            let statusEmoji = statusEmoji representative
            let uniqueId = stableRandomId id
            $"| {statusEmoji} [{label}](#user-content-{uniqueId}) | {duration.HumanizeAbbreviated()} |" |> append
        )
        let (cost, gain) =
            reportGroups
            |> List.fold (fun (cost, gain) (_, _, representative) ->
                let originSummary = originSummaries[representative.Id]
                match originSummary with
                | Some (origin, summary) ->
                    let duration = summary.Duration
                    if origin = Origin.Local then cost + duration, gain
                    else cost, gain + duration
                | _ -> cost, gain
            ) (TimeSpan.Zero, TimeSpan.Zero)
        $"| Total Cost | {cost.HumanizeAbbreviated()} |" |> append
        $"| Total Gain | {gain.HumanizeAbbreviated()} |" |> append
        if options.WhatIf |> not then
            let duration = summary.EndedAt - options.StartedAt
            $"| Duration | {duration.HumanizeAbbreviated()} |" |> append

        "" |> append

        let getNodeStatus (node: GraphDef.Node) =
            match originSummaries |> Map.tryFind node.Id with
            | Some _ -> statusEmoji node
            | _ -> Iconography.task_pending

        let getOrigin (node: GraphDef.Node) =
            summary.Nodes |> Map.tryFind node.Id |> Option.map (fun nodeInfo -> nodeInfo.Request)

        let graph = { graph with Nodes = nodes |> Seq.map (fun node -> node.Id, node) |> Map.ofSeq }
        let mermaid = Mermaid.render (Some getNodeStatus) (Some getOrigin) graph
        $"# Build Graph" |> append
        "```mermaid" |> append
        mermaid |> appendLines
        "```" |> append

        "" |> append
        "# Details" |> append
        reportGroups
        |> Seq.filter (fun (_, _, representative) -> summary.Nodes |> Map.containsKey representative.Id)
        |> Seq.iter dumpMarkdown
        "" |> append

        "</details>" |> append
        "" |> append



    let dumpTerminal (nodes: GraphDef.Node seq) =
        let nodes = nodes |> List.ofSeq
        let reportGroups =
            nodes
            |> List.groupBy (fun node -> reportId node.Id)
            |> List.map (fun (id, groupNodes) ->
                let representative =
                    groupNodes
                    |> List.sortBy (fun n -> n.Id)
                    |> List.tryFind (fun node ->
                        let cacheEntryId = GraphDef.buildCacheKey node
                        cache.TryGetSummaryOnly false cacheEntryId |> Option.isSome)
                    |> Option.defaultValue (groupNodes |> List.minBy (fun n -> n.Id))
                id, representative)

        let dumpTerminal (id: string, node: GraphDef.Node) =
            let label =
                if isBatchReport id then $"{node.Target} [batch:{id}]"
                else $"{node.Target} {node.ProjectDir}"
            let formatEndedAt (value: System.DateTime) =
                value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)

            let getHeaderFooter success =
                let color =
                    if success then $"{Ansi.Styles.green}{Ansi.Emojis.checkmark}"
                    else $"{Ansi.Styles.red}{Ansi.Emojis.crossmark}"

                $"{color} {label}{Ansi.Styles.reset}", ""

            let (logStart, logEnd), dumpLogs =
                let cacheEntryId = GraphDef.buildCacheKey node
                let originSummary = cache.TryGetSummaryOnly false cacheEntryId
                match originSummary with
                | Some (_, summary) -> 
                    let dumpLogs () =
                        summary.Operations |> Seq.iter (fun group ->
                            group |> Seq.iter (fun step ->
                                let endedAt = formatEndedAt step.EndedAt
                                $"{Ansi.Styles.yellow}{step.MetaCommand} {Ansi.Styles.dimwhite}(exit code {step.ExitCode} - {endedAt}){Ansi.Styles.reset}"
                                |> Terminal.writeLine
                                if options.Debug then
                                    $"{Ansi.Styles.cyan}{step.Command} {step.Arguments}{Ansi.Styles.reset}" |> Terminal.writeLine
                                step.Log |> IO.readTextFile |> Terminal.write
                            )
                        )

                    getHeaderFooter summary.IsSuccessful, dumpLogs
                | _ ->
                    let dumpNoLog() = $"{Ansi.Styles.yellow}No logs available{Ansi.Styles.reset}" |> Terminal.writeLine
                    getHeaderFooter false, dumpNoLog

            logStart |> Terminal.writeLine
            dumpLogs ()
            logEnd |> Terminal.writeLine

        reportGroups
        |> Seq.iter dumpTerminal


    let dumpGitHubActions (nodes: GraphDef.Node seq) =
        let nodes = nodes |> List.ofSeq
        let reportGroups =
            nodes
            |> List.groupBy (fun node -> reportId node.Id)
            |> List.map (fun (id, groupNodes) ->
                let representative =
                    groupNodes
                    |> List.sortBy (fun n -> n.Id)
                    |> List.tryFind (fun node ->
                        let cacheEntryId = GraphDef.buildCacheKey node
                        cache.TryGetSummaryOnly false cacheEntryId |> Option.isSome)
                    |> Option.defaultValue (groupNodes |> List.minBy (fun n -> n.Id))
                id, representative)

        let dumpTerminal (id: string, node: GraphDef.Node) =
            let label =
                if isBatchReport id then $"{node.Target} [batch:{id}]"
                else $"{node.Target} {node.ProjectDir}"
            let cacheEntryId = GraphDef.buildCacheKey node
            let summary = cache.TryGetSummaryOnly false cacheEntryId
            let formatEndedAt (value: System.DateTime) =
                value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)

            match summary with
            | Some (_, summary) ->
                if summary.IsSuccessful |> not then
                    $"::error title=build failed::{label}" |> Terminal.writeLine
                    match summary.Operations |> List.tryLast with
                    | Some command ->
                        match command |> List.tryLast with
                        | Some operation ->
                            let endedAt = formatEndedAt operation.EndedAt
                            $"::group::{operation.MetaCommand} (exit code {operation.ExitCode} - {endedAt})"
                            |> Terminal.writeLine
                            operation.Log |> IO.readTextFile |> Terminal.write
                            $"::endgroup::" |> Terminal.writeLine
                        | _ -> ()
                    | _ -> ()
            | None -> ()

        reportGroups
        |> Seq.iter dumpTerminal


    let logger =
        let dump nodes logType =
            match logType with
            | Contracts.Markdown filename -> dumpMarkdown filename nodes
            | Contracts.Terminal -> dumpTerminal nodes
            | Contracts.GitHubActions -> dumpGitHubActions nodes
        fun nodes -> options.LogTypes |> List.iter (dump nodes)

    $"{Ansi.Emojis.eyes} Logs" |> Terminal.writeLine
    sortedNodes |> logger
