module Progress
open System
open Ansi.Styles
open Ansi.Emojis
open Serilog

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
            $"{green} {icon} {reset}"
        | ProgressStatus.Fail restored ->
            let icon = if restored then clockwise else crossmark
            $"{red} {icon} {reset}"
        | ProgressStatus.Running (startedAt, spinner, frequency) ->
            let diff = ((DateTime.UtcNow - startedAt).TotalMilliseconds / frequency) |> int
            let offset = diff % spinner.Length
            $"{yellow} {spinner[offset]}{reset}"

    let printableItem item =
        let status = printableStatus item
        $"{status} {item.Label}"

    let refresh () =
        if Terminal.supportAnsi then
            // update status: move home, move top, write status
            try
                Ansi.beginSyncUpdate |> Terminal.write

                for item in items do
                    $"{Ansi.cursorHome}{Ansi.cursorUp 1}{item |> printableStatus}" |> Terminal.write

                $"{Ansi.cursorHome}{Ansi.cursorDown items.Length}" |> Terminal.write
            finally
                Ansi.endSyncUpdate |> Terminal.write

    let update id label status =
        match items |> List.tryFindIndex (fun item -> item.Id = id) with
        | Some index ->
            if label <> "" then failwith "Updating label for existing item is not supported"
            items[index].Status <- status
        | _ ->
            let item = { Id = id; Label = label; Status = status }
            items <- item :: items
            printableItem item |> Terminal.writeLine

        // FIXME: can't understand why refresh must be invoked here :-(
        //        if not invoked, status of item is sometimes not correctly rendered.
        //        refresh is invoked in a timer so this shall not be required.
        refresh()

    member _.Refresh () =
        refresh()

    member _.Create (id: string) (label: string) (spinner: string) (frequency: double) =
        let status = ProgressStatus.Running (DateTime.UtcNow, spinner, frequency)
        update id label status

    member _.Update (id: string) (spinner: string) (frequency: double) =
        let status = ProgressStatus.Running (DateTime.UtcNow, spinner, frequency)
        update id "" status

    member _.Complete (id: string) (success: bool) (restored: bool)=
        let status =
            if success then ProgressStatus.Success restored
            else ProgressStatus.Fail restored
        update id "" status
