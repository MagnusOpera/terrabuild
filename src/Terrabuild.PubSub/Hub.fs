namespace Terrabuild.PubSub
open System
open System.Collections.Generic
open System.Collections.Concurrent
open System.Runtime.ExceptionServices
open System.Threading
open Lock


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
    let signalLock = Lock()

    interface ISignal with
        member _.Name = name
        member _.IsRaised() = lock signalLock (fun () -> raised.IsSome )
        member _.Subscribe(onCompleted: SignalCompleted) =
            if eventQueue.HasError then
                ()
            else
                let enqueue =
                    lock signalLock (fun () ->
                        match raised with
                        | Some _ -> true
                        | _ ->
                            subscribers.Enqueue(onCompleted)
                            false)
                if enqueue then eventQueue.Enqueue kind onCompleted
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
            with get () = lock signalLock (fun () -> 
                match raised with
                | Some raised -> raised
                | _ -> Errors.raiseBugError $"Signal '{(this :> ISignal).Name}' is not raised")
            and set value =
                let notifications =
                    lock signalLock (fun () ->
                        match raised with
                        | Some _ -> Errors.raiseBugError $"Signal '{(this :> ISignal).Name}' is already raised"
                        | _ ->
                            raised <- Some value
                            let notifications = ResizeArray<SignalCompleted>(subscribers.Count)
                            while subscribers.Count > 0 do
                                notifications.Add(subscribers.Dequeue())
                            notifications)
                for subscriber in notifications do
                    eventQueue.Enqueue kind subscriber


type private Subscription(label:string, eventQueue: IEventQueue, kind: Priority, signals: ISignal list, handler: SignalCompleted) as this =
    let mutable count = signals.Length
    let mutable completed = false
    let subscriptionLock = Lock()
    do
        if count = 0 then this.Complete()
        else signals |> Seq.iter (fun signal -> signal.Subscribe(this.Callback))
    member _.Label = label
    member _.IsCompleted = Volatile.Read(&completed)
    member _.AwaitedSignals = signals
    member private _.Complete() =
        let schedule =
            lock subscriptionLock (fun () ->
                if completed then false
                else
                    completed <- true
                    true)
        if schedule then eventQueue.Enqueue kind handler
    member private _.Callback() =
        let count = lock subscriptionLock (fun () -> count <- count - 1; count)
        match count with
        | 0 -> this.Complete()
        | _ -> ()
 

[<RequireQualifiedAccess>]
type Status =
    | Ok
    | UnfulfilledSubscription of subscription:string * awaitedSignals:Set<string>
    | SubscriptionError of edi:ExceptionDispatchInfo

type IHub =
    inherit IDisposable
    abstract GetSignal<'T>: name:string -> ISignal
    abstract Subscribe: label:string -> signals:ISignal list -> handler:SignalCompleted -> unit
    abstract SubscribeBackground: label:string -> signals:ISignal list -> handler:SignalCompleted -> unit
    abstract WaitCompletion: unit -> Status




type Hub(maxConcurrency) =
    let eventQueue = new EventQueue(maxConcurrency) :> IEventQueue
    let signals = ConcurrentDictionary<string, ISignal>()
    let subscriptions = ConcurrentDictionary<int64, Subscription>()
    let mutable nextSubscriptionId = 0L

    member private _.GetSignal<'T> name =
        let getOrAdd _ = Signal<'T>(name, eventQueue, Priority.Normal) :> ISignal
        let signal = signals.GetOrAdd(name, getOrAdd)
        match signal with
        | :? Signal<'T> as signal -> signal
        | _ -> Errors.raiseBugError "Unexpected Signal type"

    member private _.Subscribe label signals kind handler =
        if eventQueue.HasError then
            ()
        else
            let id = Interlocked.Increment(&nextSubscriptionId)
            let subscription = Subscription(label, eventQueue, kind, signals, handler)
            subscriptions.TryAdd(id, subscription) |> ignore

    interface IDisposable with
        member _.Dispose () =
            eventQueue.Dispose()

    interface IHub with
        member this.GetSignal<'T>(name) = this.GetSignal<'T> name
        member this.Subscribe label signals handler = this.Subscribe label signals Priority.Normal handler
        member this.SubscribeBackground label signals handler = this.Subscribe label signals Priority.Background handler
        member _.WaitCompletion() =
            match eventQueue.WaitCompletion() with
            | Some exn -> Status.SubscriptionError exn
            | _ ->
                match subscriptions.Values |> Seq.tryFind (fun subscription -> subscription.IsCompleted |> not) with
                | Some subscription ->
                    let unraisedSignals =
                        subscription.AwaitedSignals |> Seq.filter (fun signal -> signal.IsRaised() |> not)
                        |> Seq.map (fun signal -> signal.Name)
                        |> Set.ofSeq
                    Status.UnfulfilledSubscription (subscription.Label, unraisedSignals)
                | _ -> Status.Ok

with
    static member Create maxConcurrency = new Hub(maxConcurrency) :> IHub
