namespace Terrabuild.PubSub
open System
open System.Collections.Generic
open System.Collections.Concurrent
open System.Runtime.ExceptionServices
open Terrabuild.EventQueue


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
                | Some _ -> eventQueue.Enqueue(kind, onCompleted)
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
                            eventQueue.Enqueue(kind, subscriber)
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
    | SubscriptionError of edi:ExceptionDispatchInfo

type IHub =
    inherit IDisposable
    abstract GetSignal<'T>: name:string -> ISignal
    abstract Subscribe: label:string -> signals:ISignal list -> handler:SignalCompleted -> unit
    abstract SubscribeBackground: label:string -> signals:ISignal list -> handler:SignalCompleted -> unit
    abstract WaitCompletion: unit -> Status




type Hub(maxConcurrency) =
    let eventQueue = new EventQueue(maxConcurrency)
    let signals = ConcurrentDictionary<string, ISignal>()
    let subscriptions = ConcurrentDictionary<string, Subscription>()

    member private _.GetSignal<'T> name =
        let getOrAdd _ = Signal<'T>(name, eventQueue, Priority.Normal) :> ISignal
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

    interface IDisposable with
        member _.Dispose () =
            eventQueue.Dispose()

    interface IHub with
        member this.GetSignal<'T>(name) = this.GetSignal<'T> name
        member this.Subscribe label signals handler = this.Subscribe label signals Priority.Normal handler
        member this.SubscribeBackground label signals handler = this.Subscribe label signals Priority.Background handler
        member _.WaitCompletion() =
            match eventQueue.WaitCompletion() with
            | NonNull exn -> Status.SubscriptionError exn
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
    static member Create maxConcurrency = new Hub(maxConcurrency) :> IHub
