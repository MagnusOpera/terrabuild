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
            // keep a trailing space so width stays stable
            $"{yellow} {spinner[offset]} {reset}"

    let printableItem item =
        $"{printableStatus item} {item.Label}"

    let refresh () =
        if Terminal.supportAnsi && items.Length > 0 then
            try
                Ansi.restoreCursor |> Terminal.write
                Ansi.beginSyncUpdate |> Terminal.write

                // Rewrite each line: go up 1, clear line, write full line
                for item in items do
                    $"{Ansi.cursorHome}{Ansi.cursorUp 1}{Ansi.eraseLine}{printableItem item}"
                    |> Terminal.write

                // Go back below the block
                $"{Ansi.cursorHome}{Ansi.cursorDown items.Length}"
                |> Terminal.write

            finally
                Ansi.endSyncUpdate |> Terminal.write
                // Save anchor again (cursor is now below the block)
                Ansi.saveCursor |> Terminal.write
                Terminal.flush()

    let update id label status =
        let item, isUpdate =
            match items |> List.tryFindIndex (fun item -> item.Id = id) with
            | Some index ->
                if label <> "" then failwith "Updating label for existing item is not supported"
                items[index].Status <- status
                items[index], true
            | _ ->
                let item = { Id = id; Label = label; Status = status }
                items <- item :: items
                item, false

        if not Terminal.supportAnsi then
            printableItem item |> Terminal.writeLine
        else
            if not isUpdate then
                // New item prints a line (extends the progress block)
                printableItem item |> Terminal.writeLine

                // Cursor is now below the block: save anchor
                Ansi.saveCursor |> Terminal.write
                Terminal.flush()
            else
                // Update: just refresh the whole block
                refresh ()

    member _.Refresh () =
        refresh ()

    member _.Create (id: string) (label: string) (spinner: string) (frequency: double) =
        let status = ProgressStatus.Running (DateTime.UtcNow, spinner, frequency)
        update id label status

    member _.Update (id: string) (spinner: string) (frequency: double) =
        let status = ProgressStatus.Running (DateTime.UtcNow, spinner, frequency)
        update id "" status

    member _.Complete (id: string) (restored: bool) (success: bool) =
        let status =
            if success then ProgressStatus.Success restored
            else ProgressStatus.Fail restored
        update id "" status
