module Progress
open System
open Ansi.Styles
open Ansi.Emojis

[<RequireQualifiedAccess>]
type ProgressStatus =
    | Success of restored:bool
    | Fail of restored:bool
    | Running of startedAt:DateTime * spinner:string * frequency:double

type ProgressItem = {
    Id: string
    mutable Label: string
    mutable Status: ProgressStatus
}

type ProgressRenderer() =
    let mutable items = []

    // // https://antofthy.gitlab.io/info/ascii/HeartBeats_howto.txt
    // let spinnerWaiting = [ "⠁"; "⠂"; "⠄"; "⠂" ]
    // let frequencyWaiting = 200.0

    // let spinnerUpload = [ "⣤"; "⠶"; "⠛"; "⠛"; "⠶" ]
    // let frequencyUpload = 200.0

    // let spinnerProgress = [ "⠋"; "⠙"; "⠹"; "⠸"; "⠼"; "⠴";  "⠦"; "⠧"; "⠇"; "⠏" ]
    // let frequencyProgress = 100.0

    let printableStatus item =
        match item.Status with
        | ProgressStatus.Success restored ->
            let icon = if restored then clockwise else checkmark            
            green + " " + icon + reset
        | ProgressStatus.Fail restored ->
            let icon = if restored then clockwise else crossmark
            red + " " + icon + reset
        | ProgressStatus.Running (startedAt, spinner, frequency) ->
            let diff = ((DateTime.UtcNow - startedAt).TotalMilliseconds / frequency) |> int
            let offset = diff % spinner.Length
            $"{yellow} {spinner[offset]}{reset}"

    let printableItem item =
        let status = printableStatus item
        $"{status} {item.Label}"

    member _.Refresh () =
        if Terminal.supportAnsi then
            // update status: move home, move top, write status
            let updateCmd =
                items
                |> List.fold (fun acc item -> acc + $"{Ansi.cursorHome}{Ansi.cursorUp 1}" + (item |> printableStatus)) ""
            let updateCmd = Ansi.beginSyncUpdate + updateCmd + $"{Ansi.cursorHome}{Ansi.cursorDown items.Length}" + Ansi.endSyncUpdate
            updateCmd |> Terminal.write |> Terminal.flush

    member _.Update (id: string) (label: string) (spinner: string) (frequency: double) =
        match items |> List.tryFindIndex (fun item -> item.Id = id) with
        | Some index ->
            items[index].Status <- ProgressStatus.Running (DateTime.UtcNow, spinner, frequency)

            if Terminal.supportAnsi |> not then
                printableItem items[index] |> Terminal.writeLine |> Terminal.flush

        | _ ->
            let item = { Id = id; Label = label; Status = ProgressStatus.Running (DateTime.UtcNow, spinner, frequency) }
            items <- item :: items
            printableItem item |> Terminal.writeLine |> Terminal.flush

    member _.Complete (id: string) (label: string) (success: bool) (restored: bool)=
        let status =
            if success then ProgressStatus.Success restored
            else ProgressStatus.Fail restored

        let item =
            match items |> List.tryFindIndex (fun item -> item.Id = id) with
            | Some index ->
                items[index].Status <- status
                items[index]
            | _ ->
                let item = { Id = id; Label = label; Status = status }
                items <- item :: items
                item

        if Terminal.supportAnsi |> not then
            printableItem item |> Terminal.writeLine |> Terminal.flush
