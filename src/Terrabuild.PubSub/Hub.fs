namespace Terrabuild.PubSub
open System
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading


type private Priority =
    | Normal
    | Background

type private IEventQueue =
    abstract Enqueue: kind:Priority -> action:(unit -> unit) -> unit

type private EventQueue(maxConcurrency: int) as this =
    let completed = new ManualResetEvent(false)
    let normalQueue = Queue<(unit -> unit)>()
    let backgroundQueue = Queue<(unit -> unit)>()
    let mutable isStarted = false
    let mutable totalTasks = 0
    let mutable lastError = None
    let inFlightNormalTasks = ref 0
    let inFlightBackgroundTasks = ref 0

    // NOTE: always take the lock before calling trySchedule
    let rec trySchedule () =

        let schedule (count: ref<int>) action =
            count.Value <- count.Value + 1
            async {
                let error = Errors.tryInvoke action
                lock this (fun () ->
                    error |> Option.iter (fun error ->
                        lastError <- Some error
                        normalQueue.Clear())
                    count.Value <- count.Value - 1
                    trySchedule()
                )
            } |> Async.Start
            trySchedule() // try to schedule more tasks if possible

        let totalInFlight = inFlightNormalTasks.Value + inFlightBackgroundTasks.Value
        let canAcceptTask = totalInFlight < 2 * maxConcurrency
        let canScheduleNormalTask = lastError = None && 0 < normalQueue.Count && inFlightNormalTasks.Value < maxConcurrency
        let canScheduleBackgroundTask = 0 < backgroundQueue.Count && inFlightBackgroundTasks.Value < 2 * maxConcurrency

        if canAcceptTask && canScheduleBackgroundTask then schedule inFlightBackgroundTasks (backgroundQueue.Dequeue())
        elif canAcceptTask && canScheduleNormalTask then schedule inFlightNormalTasks (normalQueue.Dequeue())
        elif totalInFlight = 0 then completed.Set() |> ignore

    interface IEventQueue with
        member _.Enqueue kind action =
            lock this (fun () ->
                totalTasks <- totalTasks + 1
                match kind with
                | Normal -> normalQueue.Enqueue(action)
                | Background -> backgroundQueue.Enqueue(action)
                if isStarted then trySchedule()
            )

    member _.WaitCompletion() =
        let totalTasks = lock this (fun () ->
            isStarted <- true
            if totalTasks > 0 then
                async {
                    lock this trySchedule
                } |> Async.Start
            totalTasks
        )
        if totalTasks > 0 then completed.WaitOne() |> ignore
        lastError

type SignalCompleted = unit -> unit

type ISignal =
    abstract Name: string
    abstract IsRaised: unit -> bool
    abstract Subscribe: SignalCompleted -> unit
    abstract Get<'T>: unit -> 'T
    abstract Set<'T>: 'T -> unit

and ISignal<'T> =
    inherit ISignal
    abstract Value: 'T with get, set

type private Signal<'T>(name, eventQueue: IEventQueue, kind: Priority) as this =
    let subscribers = Queue<SignalCompleted>()
    let mutable raised = None

    interface ISignal with
        member _.Name = name
        member _.IsRaised() = lock this (fun () -> raised.IsSome )
        member _.Subscribe(onCompleted: SignalCompleted) =
            lock this (fun () ->
                match raised with
                | Some _ -> eventQueue.Enqueue kind onCompleted
                | _ -> subscribers.Enqueue(onCompleted)
            )
        member _.Get<'Q>() =
            match box this with
            | :? ISignal<'Q> as signal -> signal.Value
            | _ -> Errors.raiseBugError $"Unexpected Signal type {typeof<'Q>.Name}"

        member _.Set<'Q>(value: 'Q) = 
            match box this with
            | :? ISignal<'Q> as signal -> signal.Value <- value
            | _ -> Errors.raiseBugError $"Unexpected Signal type {typeof<'Q>.Name}"

    interface ISignal<'T> with
        member _.Value
            with get () = lock this (fun () -> 
                match raised with
                | Some raised -> raised
                | _ -> Errors.raiseBugError $"Signal '{(this :> ISignal).Name}' is not raised")
            and set value = lock this (fun () ->
                match raised with
                | Some _ -> Errors.raiseBugError $"Signal '{(this :> ISignal).Name}' is already raised"
                | _ -> 
                    let rec notify() =
                        match subscribers.TryDequeue() with
                        | true, subscriber ->
                            eventQueue.Enqueue kind subscriber
                            notify()
                        | _ -> ()
                    raised <- Some value
                    notify())


type private Subscription(label:string, signal: ISignal<Unit>, signals: ISignal list) as this =
    let mutable count = signals.Length
    do
        if count = 0 then signal.Value <- ()
        else signals |> Seq.iter (fun signal -> signal.Subscribe(this.Callback))
    member _.Label = label
    member _.Signal = signal
    member _.AwaitedSignals = signals
    member private _.Callback() =
        let count = lock this (fun () -> count <- count - 1; count)
        match count with
        | 0 -> signal.Value <- ()
        | _ -> ()
 

[<RequireQualifiedAccess>]
type Status =
    | Ok
    | UnfulfilledSubscription of subscription:string * awaitedSignals:Set<string>
    | SubscriptionError of exn:Exception

type IHub =
    abstract GetSignal<'T>: name:string -> ISignal
    abstract Subscribe: label:string -> signals:ISignal list -> handler:SignalCompleted -> unit
    abstract SubscribeBackground: label:string -> signals:ISignal list -> handler:SignalCompleted -> unit
    abstract WaitCompletion: unit -> Status


type Hub(maxConcurrency) =
    let eventQueue = EventQueue(maxConcurrency)
    let signals = ConcurrentDictionary<string, ISignal>()
    let subscriptions = ConcurrentDictionary<string, Subscription>()

    member private _.GetSignal<'T> name =
        let getOrAdd _ = Signal<'T>(name, eventQueue, Normal) :> ISignal
        let signal = signals.GetOrAdd(name, getOrAdd)
        match signal with
        | :? Signal<'T> as signal -> signal
        | _ -> Errors.raiseBugError "Unexpected Signal type"

    member private _.Subscribe label signals kind handler =
        let name = Guid.NewGuid().ToString()
        let signal = Signal<Unit>(name, eventQueue, kind)
        let subscription = Subscription(label, signal :> ISignal<Unit>, signals)
        subscriptions.TryAdd(name, subscription) |> ignore
        (signal :> ISignal).Subscribe(handler)

    interface IHub with
        member this.GetSignal<'T>(name) = this.GetSignal<'T> name
        member this.Subscribe label signals handler = this.Subscribe label signals Normal handler
        member this.SubscribeBackground label signals handler = this.Subscribe label signals Background handler
        member _.WaitCompletion() =
            match eventQueue.WaitCompletion() with
            | Some exn -> Status.SubscriptionError exn
            | _ ->
                match subscriptions.Values |> Seq.tryFind (fun subscription -> subscription.Signal.IsRaised() |> not) with
                | Some subscription ->
                    let unraisedSignals =
                        subscription.AwaitedSignals |> Seq.filter (fun signal -> signal.IsRaised() |> not)
                        |> Seq.map (fun signal -> signal.Name)
                        |> Set.ofSeq
                    Status.UnfulfilledSubscription (subscription.Label, unraisedSignals)
                | _ -> Status.Ok

with
    static member Create maxConcurrency = Hub(maxConcurrency) :> IHub
