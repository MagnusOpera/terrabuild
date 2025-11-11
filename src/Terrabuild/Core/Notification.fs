module Notification
open System.Threading

// https://antofthy.gitlab.io/info/ascii/HeartBeats_howto.txt
let spinnerScheduled = "⠁⠂⠄⠂"
let frequencyScheduled = 200.0

let spinnerUpload = "↑  ↑ ↑ "
let frequencyUpload = 200.0

let spinnerDownload = "↓  ↓ ↓ "
let frequencyDownload = 200.0

let spinnerBuilding = "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏"
let frequencyBuilding = 100.0


[<RequireQualifiedAccess>]
type TaskStatus =
    | Scheduled
    | Building
    | Downloading
    | Uploading

[<RequireQualifiedAccess>]
type PrinterProtocol =
    | BuildStarted
    | BuildCompleted
    | TaskScheduled of taskId:string * label:string
    | BatchScheduled of (string*string) list
    | TaskStatusChanged of taskId:string * status:TaskStatus
    | TaskCompleted of taskId:string * restore:bool * success:bool
    | Render

type BuildNotification() =

    let buildComplete = new ManualResetEvent(false)
    let renderer = Progress.ProgressRenderer()
    let updateTimer = System.TimeSpan.FromMilliseconds(100L)

    let handler (inbox: MailboxProcessor<PrinterProtocol>) =

        // timer to update display
        let cts = new CancellationTokenSource()
        task {
            use timer = new PeriodicTimer(updateTimer)
            while! timer.WaitForNextTickAsync(cts.Token) do PrinterProtocol.Render |> inbox.Post
        } |> Async.AwaitTask |> Async.Start

        // the message processing function
        let rec messageLoop () = async {
            let! msg = inbox.Receive()
            match msg with
            | PrinterProtocol.BuildStarted -> 
                return! messageLoop () 

            | PrinterProtocol.BuildCompleted ->
                cts.Cancel()
                renderer.Refresh ()
                buildComplete.Set() |> ignore

            | PrinterProtocol.TaskScheduled (taskId, label) ->
                renderer.Create taskId label spinnerScheduled frequencyScheduled
                return! messageLoop ()

            | PrinterProtocol.BatchScheduled batch ->
                for (taskId, label) in batch do
                    renderer.Create taskId label spinnerScheduled frequencyScheduled
                return! messageLoop ()

            | PrinterProtocol.TaskStatusChanged (taskId, status) ->
                let spinner, frequency =
                    match status with
                    | TaskStatus.Scheduled -> spinnerScheduled, frequencyScheduled
                    | TaskStatus.Downloading -> spinnerDownload, frequencyDownload
                    | TaskStatus.Uploading -> spinnerUpload, frequencyUpload
                    | TaskStatus.Building -> spinnerBuilding, frequencyBuilding
                renderer.Update taskId spinner frequency
                return! messageLoop ()

            | PrinterProtocol.TaskCompleted (taskId, restore, success) ->
                renderer.Complete taskId restore success
                return! messageLoop ()

            | PrinterProtocol.Render ->
                renderer.Refresh ()
                return! messageLoop ()
        }

        // start the loop
        messageLoop()

    let printerAgent = MailboxProcessor.Start(handler)

    interface BuildProgress.IBuildProgress with
        member _.BuildStarted () =
            PrinterProtocol.BuildStarted
            |> printerAgent.Post

        member _.BuildCompleted () = 
            PrinterProtocol.BuildCompleted
            |> printerAgent.Post
            buildComplete.WaitOne() |> ignore

        member _.TaskScheduled (taskId:string) (label:string) =
            PrinterProtocol.TaskScheduled (taskId, label)
            |> printerAgent.Post

        member _.BatchScheduled(tasks: (string * string) list) = 
            PrinterProtocol.BatchScheduled tasks
            |> printerAgent.Post

        member _.TaskDownloading (taskId:string) = 
            PrinterProtocol.TaskStatusChanged (taskId, TaskStatus.Downloading)
            |> printerAgent.Post

        member _.TaskBuilding (taskId:string) = 
            PrinterProtocol.TaskStatusChanged (taskId, TaskStatus.Building)
            |> printerAgent.Post

        member _.TaskUploading (taskId:string) = 
            PrinterProtocol.TaskStatusChanged (taskId, TaskStatus.Uploading)
            |> printerAgent.Post

        member _.TaskCompleted (taskId:string) (restore: bool) (success:bool)= 
            PrinterProtocol.TaskCompleted (taskId, restore, success)
            |> printerAgent.Post        
