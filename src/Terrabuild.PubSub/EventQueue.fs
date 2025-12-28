namespace Terrabuild.PubSub

open System
open System.Runtime.ExceptionServices
open System.Threading
open System.Threading.Channels
open System.Threading.Tasks
open Lock

type Priority =
    | Normal = 0
    | Background = 1

type IEventQueue =
    inherit IDisposable
    abstract Enqueue: kind: Priority -> action: (unit -> unit) -> unit
    abstract WaitCompletion: unit -> ExceptionDispatchInfo option

[<Struct>]
type private WorkItem =
    val Kind: Priority
    val Run: unit -> unit

    new (kind: Priority, run: unit -> unit) =
        { Kind = kind; Run = run }

type EventQueue(maxConcurrency: int) =
    do
        if maxConcurrency <= 0 then
            invalidArg (nameof maxConcurrency) "maxConcurrency must be > 0"

    let maxConcurrency = maxConcurrency
    let backgroundMaxConcurrency = 4 * maxConcurrency

    let options =
        UnboundedChannelOptions(
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = true
        )

    let normal = Channel.CreateUnbounded<WorkItem>(options)
    let background = Channel.CreateUnbounded<WorkItem>(options)

    // gate is only for start + first error transition, not hot path
    let gate = Lock()

    let mutable started = false
    let mutable pending = 0
    let mutable lastError : ExceptionDispatchInfo option = None

    let workers : Task array ref = ref [||]

    let drained =
        TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)

    let cts = new CancellationTokenSource()

    let decrementPending () =
        let value = Interlocked.Decrement(&pending)
        if started && value = 0 then
            drained.TrySetResult() |> ignore

    let drainAndDrop (reader: ChannelReader<WorkItem>) =
        let mutable item = Unchecked.defaultof<WorkItem>
        while reader.TryRead(&item) do
            decrementPending()

    let trySetError (ex: exn) =
        // fast path: already failed
        if (Volatile.Read(&lastError) |> Option.isSome) then
            ()
        else
            lock gate (fun () ->
                match lastError with
                | Some _ -> ()
                | None ->
                    lastError <- Some (ExceptionDispatchInfo.Capture ex)

                    // IMPORTANT: do not fault the channel, otherwise WaitToReadAsync throws
                    normal.Writer.TryComplete() |> ignore

                    // Drop already-enqueued normal tasks and fix pending
                    drainAndDrop normal.Reader
            )

    let run (item: WorkItem) =
        try
            try
                item.Run()
            with ex ->
                // any error sets lastError + clears normal queue (stops normal scheduling)
                trySetError ex
        finally
            decrementPending()

    let workerLoop (reader: ChannelReader<WorkItem>) =
        task {
            try
                let token = cts.Token
                let mutable item = Unchecked.defaultof<WorkItem>

                while! reader.WaitToReadAsync(token) do
                    while reader.TryRead(&item) do
                        run item
            with
            | :? OperationCanceledException ->
                ()
            | :? ChannelClosedException ->
                ()
        } :> Task

    let startWorkers () =
        let tasks = Array.zeroCreate<Task> (maxConcurrency + backgroundMaxConcurrency)

        let mutable i = 0
        while i < maxConcurrency do
            tasks[i] <- Task.Run(fun () -> workerLoop normal.Reader, cts.Token)
            i <- i + 1

        while i < tasks.Length do
            tasks[i] <- Task.Run(fun () -> workerLoop background.Reader, cts.Token)
            i <- i + 1

        tasks

    let ensureStarted () =
        if started then
            ()
        else
            lock gate (fun () ->
                if started then
                    ()
                else
                    started <- true

                    // If nothing pending at start, we are already drained
                    if Volatile.Read(&pending) = 0 then
                        drained.TrySetResult() |> ignore

                    workers.Value <- startWorkers ()
            )

    interface IEventQueue with
        member _.Enqueue kind action =
            if isNull (box action) then
                nullArg (nameof action)

            // Once we have an error, normal work is dropped.
            if kind = Priority.Normal && (Volatile.Read(&lastError) |> Option.isSome) then
                ()
            else
                Interlocked.Increment(&pending) |> ignore

                let work = WorkItem(kind, action)

                let writer =
                    if kind = Priority.Normal then normal.Writer else background.Writer

                // Very fast for unbounded channels
                if not (writer.TryWrite work) then
                    // Extremely rare for unbounded, but safe fallback
                    writer.WriteAsync(work, cts.Token).AsTask()
                        .ContinueWith(
                            (fun (_: Task) ->
                                // If WriteAsync failed, mark as complete so WaitCompletion doesn't hang.
                                decrementPending()
                            ),
                            TaskScheduler.Default
                        )
                    |> ignore

        member _.WaitCompletion() =
            ensureStarted ()

            // Wait until everything accepted has finished
            drained.Task.GetAwaiter().GetResult()

            // Close channels so workers exit
            normal.Writer.TryComplete() |> ignore
            background.Writer.TryComplete() |> ignore

            // Ensure workers are done
            Task.WaitAll(workers.Value)

            lastError

        member _.Dispose() =
            cts.Cancel()

            normal.Writer.TryComplete() |> ignore
            background.Writer.TryComplete() |> ignore

            try
                if workers.Value.Length > 0 then
                    Task.WaitAll(workers.Value)
            with
            | _ -> ()

            cts.Dispose()
