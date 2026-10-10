module Terrabuild.PubSub.PerformanceTests

open System
open System.Diagnostics
open System.Threading
open NUnit.Framework
open Terrabuild.PubSub

let private measure name action =
    for sample in 0..10 do
        let allocated = GC.GetTotalAllocatedBytes(true)
        let timer = Stopwatch.StartNew()
        action ()
        timer.Stop()
        if sample >= 4 then
            printfn "PUBSUB_BENCH workload=%s sample=%d elapsedMs=%.3f allocatedBytes=%d"
                name (sample - 3) timer.Elapsed.TotalMilliseconds
                (GC.GetTotalAllocatedBytes(true) - allocated)

[<Test; Explicit("Measures PubSub scheduling separately from build tools")>]
let BenchmarkPubSub() =
    measure "published-reads" (fun () ->
        use hub = Hub.Create(4)
        let signal = hub.GetSignal<int> "published"
        signal.Set(42)
        let mutable total = 0
        for _ in 1..500000 do
            total <- total + signal.Get<int>()
        Assert.That(total, Is.EqualTo(21000000)))

    measure "fan-in" (fun () ->
        use hub = Hub.Create(4)
        let mutable completed = 0
        let signals =
            Array.init 4000 (fun index ->
                let dependencies =
                    List.init 8 (fun dependency -> hub.GetSignal<int> $"{index}-{dependency}")
                hub.Subscribe $"{index}" dependencies (fun () ->
                    Interlocked.Increment(&completed) |> ignore)
                dependencies)
        for dependencies in signals do
            for signal in dependencies do signal.Set(42)
        Assert.That(hub.WaitCompletion(), Is.EqualTo(Status.Ok))
        Assert.That(completed, Is.EqualTo(4000)))

[<Test>]
let ConcurrentPublicationCompletesEachSubscriptionOnce() =
    use hub = Hub.Create(4)
    let dependencies = List.init 32 (fun index -> hub.GetSignal<int> $"signal-{index}")
    let mutable completed = 0
    for index in 1..200 do
        hub.Subscribe $"subscriber-{index}" dependencies (fun () ->
            for signal in dependencies do
                Assert.That(signal.Get<int>(), Is.EqualTo(42))
            Interlocked.Increment(&completed) |> ignore)
    System.Threading.Tasks.Parallel.ForEach(dependencies, fun (signal: ISignal) -> signal.Set(42)) |> ignore
    Assert.That(hub.WaitCompletion(), Is.EqualTo(Status.Ok))
    Assert.That(completed, Is.EqualTo(200))

[<Test>]
let PublishedNullIsStillRaisedAndCannotBeOverwritten() =
    use hub = Hub.Create(1)
    let signal = hub.GetSignal<string | null> "null"
    signal.Set<string | null>(null)
    Assert.That(signal.IsRaised(), Is.True)
    Assert.That(signal.Get<string | null>(), Is.Null)
    Assert.Throws<Errors.TerrabuildException>(Action(fun () -> signal.Set<string | null>("second"))) |> ignore
    let mutable completed = false
    hub.Subscribe "late" [signal] (fun () -> completed <- true)
    Assert.That(hub.WaitCompletion(), Is.EqualTo(Status.Ok))
    Assert.That(completed, Is.True)

type private PublishedPayload = { Tag: string; Values: int array }

[<Test>]
let ConcurrentSubscribersObserveInitializedReferencePayload() =
    for iteration in 1..20 do
        use hub = Hub.Create(4)
        let signal = hub.GetSignal<PublishedPayload> "payload"
        let mutable completed = 0
        let subscribe index =
            hub.Subscribe $"{index}" [signal] (fun () ->
                let payload = signal.Get<PublishedPayload>()
                Assert.That(payload.Tag, Is.EqualTo("published"))
                Assert.That(payload.Values |> Array.forall (fun value -> value = iteration), Is.True)
                Interlocked.Increment(&completed) |> ignore)
        for index in 0..15 do subscribe index
        System.Threading.Tasks.Parallel.Invoke(
            Action(fun () -> signal.Set({ Tag = "published"; Values = Array.create 32 iteration })),
            Action(fun () -> System.Threading.Tasks.Parallel.For(16, 144, Action<int>(subscribe)) |> ignore))
        for index in 144..159 do subscribe index
        Assert.That(hub.WaitCompletion(), Is.EqualTo(Status.Ok))
        Assert.That(completed, Is.EqualTo(160))
