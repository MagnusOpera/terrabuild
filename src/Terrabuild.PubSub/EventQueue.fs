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
    abstract HasError: bool

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
    let backgroundMaxConcurrency = int (min (int64 Int32.MaxValue) (4L * int64 maxConcurrency))

    let options =
        UnboundedChannelOptions(
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        )

    let normal = Channel.CreateUnbounded<WorkItem>(options)
    let background = Channel.CreateUnbounded<WorkItem>(options)

    // Only worker creation and the first error transition take the gate.
    let gate = Lock()

    let mutable waiting = false
    let mutable pending = 0
    let mutable normalPending = 0
    let mutable backgroundPending = 0
    let mutable normalWorkerCount = 0
    let mutable backgroundWorkerCount = 0
    let mutable lastError : ExceptionDispatchInfo option = None

    let workers = ResizeArray<Task>()

    let drained =
        TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)

    let cts = new CancellationTokenSource()

    let decrementPending kind =
        if kind = Priority.Normal then Interlocked.Decrement(&normalPending) |> ignore
        else Interlocked.Decrement(&backgroundPending) |> ignore
        let value = Interlocked.Decrement(&pending)
        if Volatile.Read(&waiting) && value = 0 then
            drained.TrySetResult() |> ignore

    let drainAndDrop (reader: ChannelReader<WorkItem>) =
        let mutable item = Unchecked.defaultof<WorkItem>
        while reader.TryRead(&item) do
            decrementPending item.Kind

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
            decrementPending item.Kind

    // Handlers are synchronous and may wait on child processes, file locks or HTTP.
    // Dedicated, bounded workers keep those waits off the shared ThreadPool.
    let workerLoop (reader: ChannelReader<WorkItem>) =
        try
            let mutable item = Unchecked.defaultof<WorkItem>
            while reader.WaitToReadAsync(cts.Token).AsTask().GetAwaiter().GetResult() do
                while reader.TryRead(&item) do
                    run item
        with
        | :? OperationCanceledException -> ()
        | :? ChannelClosedException -> ()

    let ensureWorkers kind =
        let desiredCount () =
            if kind = Priority.Normal then min maxConcurrency (Volatile.Read(&normalPending))
            else min backgroundMaxConcurrency (Volatile.Read(&backgroundPending))
        let workerCount () =
            if kind = Priority.Normal then Volatile.Read(&normalWorkerCount)
            else Volatile.Read(&backgroundWorkerCount)
        if workerCount() < desiredCount() then
            lock gate (fun () ->
                // Grow only as work arrives, avoiding idle threads for small graphs.
                while workerCount() < desiredCount() do
                    let reader =
                        if kind = Priority.Normal then
                            normalWorkerCount <- normalWorkerCount + 1
                            normal.Reader
                        else
                            backgroundWorkerCount <- backgroundWorkerCount + 1
                            background.Reader
                    workers.Add(Task.Factory.StartNew(
                        Action(fun () -> workerLoop reader),
                        cts.Token,
                        TaskCreationOptions.LongRunning,
                        TaskScheduler.Default)))

    let workerSnapshot () = lock gate (fun () -> workers.ToArray())

    interface IEventQueue with
        member _.Enqueue kind action =
            if isNull (box action) then
                nullArg (nameof action)

            // Once we have an error, no new work is accepted.
            if Volatile.Read(&lastError) |> Option.isSome then
                ()
            else
                Interlocked.Increment(&pending) |> ignore
                if kind = Priority.Normal then Interlocked.Increment(&normalPending) |> ignore
                else Interlocked.Increment(&backgroundPending) |> ignore

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
                                decrementPending kind
                            ),
                            TaskScheduler.Default
                        )
                    |> ignore

                // Start processing immediately so producers such as workspace
                // discovery can overlap with the work they enqueue.
                ensureWorkers kind

        member _.WaitCompletion() =
            Volatile.Write(&waiting, true)
            if Volatile.Read(&pending) = 0 then
                drained.TrySetResult() |> ignore

            // Wait until everything accepted has finished
            drained.Task.GetAwaiter().GetResult()

            // Close channels so workers exit
            normal.Writer.TryComplete() |> ignore
            background.Writer.TryComplete() |> ignore

            // Ensure workers are done
            Task.WaitAll(workerSnapshot())

            lastError

        member _.HasError =
            Volatile.Read(&lastError) |> Option.isSome

        member _.Dispose() =
            cts.Cancel()

            normal.Writer.TryComplete() |> ignore
            background.Writer.TryComplete() |> ignore

            try
                Task.WaitAll(workerSnapshot())
            with
            | _ -> ()

            cts.Dispose()
