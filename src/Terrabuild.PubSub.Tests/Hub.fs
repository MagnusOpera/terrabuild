module Terrabuild.PubSub.Tests

open NUnit.Framework
open FsUnit
open System.Threading
open System.Threading.Tasks


[<Test>]
let successful() =
    use hub = Hub.Create(1)

    let value1 = hub.GetSignal<int> "computed1"
    let computed2 = hub.GetSignal<string> "computed2"

    let computed1 = hub.GetSignal<int> "computed1"
    let value2 = hub.GetSignal<string> "computed2"

    let mutable triggered0 = false
    let callback0() =
        triggered0 <- true

    let mutable triggered1 = false
    let callback1() =
        value1.Get<int>() |> should equal 42
        computed2.Set("tralala")
        triggered1 <- true

    let mutable triggered2 = false
    let mutable triggered3 = false
    let callback2() =
        value1.Get<int>() |> should equal 42
        value2.Get<string>() |> should equal "tralala"
        triggered2 <- true

        // callback3 must be immediately triggered as computed1/2 are immediately available
        let callback3() =
            // getting another computed lead to same value
            value1.Get<int>() |> should equal 42
            value2.Get<string>() |> should equal "tralala"
            hub.GetSignal<int>("computed1").Get<int>() |> should equal 42
            triggered3 <- true

        hub.Subscribe "subscription3" [ value1; value2 ] callback3


    hub.Subscribe "callback0" [] callback0
    hub.Subscribe "callback1" [ value1 ] callback1
    hub.Subscribe "callback2" [ value1; value2 ] callback2

    computed1.Set(42)

    let status = hub.WaitCompletion()

    status |> should equal Status.Ok
    value1.Get<int>() |> should equal 42
    value2.Get<string>() |> should equal "tralala"
    triggered0 |> should equal true
    triggered1 |> should equal true
    triggered2 |> should equal true
    triggered3 |> should equal true


[<Test>]
let exception_in_callback_is_error() =
    use hub = Hub.Create(1)

    let value1 = hub.GetSignal<int> "computed1"
    let computed2 = hub.GetSignal<string> "computed2"
    let value3 = hub.GetSignal<float> "computed3"

    let computed1 = hub.GetSignal<int> "computed1"
    let value2 = hub.GetSignal<string> "computed2"
    let computed3 = hub.GetSignal<float> "computed3"

    let mutable triggered1 = false
    let callback() =
        value1.Get<int>() |> should equal 42
        value2.Get<string>() |> should equal "tralala"
        triggered1 <- true
        failwith "workflow failed"

    let mutable triggered2 = false
    let neverCallback() =
        triggered2 <- true
        failwith "Callback shall never be called"

    hub.Subscribe "subscription1" [ value1; value2 ] callback
    hub.Subscribe "subscription2" [ value3 ] neverCallback

    computed1.Set(42)
    computed2.Set("tralala")

    // callback fails
    let status = hub.WaitCompletion()

    match status with
    | Status.SubscriptionError edi -> edi.SourceException.Message |> should equal "workflow failed"
    | _ -> Assert.Fail()
    value1.Get<int>() |> should equal 42
    value2.Get<string>() |> should equal "tralala"
    triggered1 |> should equal true
    triggered2 |> should equal false


[<Test>]
let unsignaled_subscription1_is_error() =
    use hub = Hub.Create(1)

    let value1 = hub.GetSignal<int> "computed1"
    let computed2 = hub.GetSignal<string> "computed2"
    let value3 = hub.GetSignal<float> "computed3"

    let computed1 = hub.GetSignal<int> "computed1"
    let value2 = hub.GetSignal<string> "computed2"
    let computed3 = hub.GetSignal<float> "computed3"

    let mutable triggered1 = false
    let callback() =
        value1.Get<int>() |> should equal 42
        value2.Get<string>() |> should equal "tralala"
        triggered1 <- true

    let mutable triggered2 = false
    let neverCallback() =
        triggered2 <- true
        failwith "Callback shall never be called"

    hub.Subscribe "subscription1" [ value1; value2 ] callback
    hub.Subscribe "subscription2" [ value3 ] neverCallback

    computed1.Set(42)
    computed2.Set("tralala")

    // computed3 is never triggered
    let status = hub.WaitCompletion()

    match status with
    | Status.UnfulfilledSubscription (subscription, signals) ->
        subscription |> should equal "subscription2"
        signals |> should equal (Set ["computed3"])
    | _ -> Assert.Fail()
    triggered1 |> should equal true
    triggered2 |> should equal false
    value1.Get<int>() |> should equal 42
    value2.Get<string>() |> should equal "tralala"
    (fun () -> value3.Get() |> ignore) |> should throw typeof<Errors.TerrabuildException>


[<Test>]
let unsignaled_subscription2_is_error() =
    use hub = Hub.Create(1)

    let value1 = hub.GetSignal<int> "computed1"
    let computed2 = hub.GetSignal<string> "computed2"
    let value3 = hub.GetSignal<float> "computed3"

    let computed1 = hub.GetSignal<int> "computed1"
    let value2 = hub.GetSignal<string> "computed2"
    let computed3 = hub.GetSignal<float> "computed3"

    let mutable triggered1 = false
    let callback() =
        value1.Get<int>() |> should equal 42
        triggered1 <- true

    let mutable triggered2 = false
    let neverCallback() =
        triggered2 <- true
        failwith "Callback shall never be called"

    hub.Subscribe "subscription1" [ value1 ] callback
    hub.Subscribe "subscription2" [ value2; value3 ] neverCallback

    computed1.Set(42)

    // computed3 is never triggered
    let status = hub.WaitCompletion()

    match status with
    | Status.UnfulfilledSubscription (subscription, signals) ->
        subscription |> should equal "subscription2"
        signals |> should equal (Set ["computed2"; "computed3"])
    | _ -> Assert.Fail()
    triggered1 |> should equal true
    triggered2 |> should equal false
    value1.Get<int>() |> should equal 42
    (fun () -> value3.Get<float>() |> ignore) |> should throw typeof<Errors.TerrabuildException>



[<Test>]
let computed_must_match_type() =
    use hub = Hub.Create(1)

    let value1 = hub.GetSignal<int> "computed1"
    (fun () -> hub.GetSignal<string> "computed1" |> ignore) |> should throw typeof<Errors.TerrabuildException>

    let computed2 = hub.GetSignal<string> "computed2"
    (fun () -> hub.GetSignal<int> "computed2" |> ignore) |> should throw typeof<Errors.TerrabuildException>



[<Test>]
let download_subscription_priority() =
    use hub = Hub.Create(2)
    let value1 = hub.GetSignal<int> "download1"
    let value2 = hub.GetSignal<int> "download2"
    let mutable triggered = false
    let callback() =
        value1.Get<int>() |> should equal 99
        value2.Get<int>() |> should equal 100
        triggered <- true
    hub.SubscribeBackground "downloadSub" [ value1; value2 ] callback
    value1.Set(99)
    value2.Set(100)
    let status = hub.WaitCompletion()
    status |> should equal Status.Ok
    triggered |> should equal true



[<Test>]
let error_should_prevent_scheduling_new_tasks() =
    use hub = Hub.Create(1)

    let value1 = hub.GetSignal<int> "v1"
    let value2 = hub.GetSignal<int> "v2"

    // This callback will fail
    let failingCallback() =
        value1.Get<int>() |> ignore
        failwith "boom"

    let mutable triggeredNever = false
    let neverCallback() =
        triggeredNever <- true
        failwith "This callback must never be triggered"

    hub.Subscribe "failSub" [ value1 ] failingCallback
    hub.Subscribe "neverSub" [ value2 ] neverCallback

    // Signal v1 triggers failSub (raises boom)
    value1.Set(123)
    // v2 would normally trigger neverSub, but scheduling should stop after error
    value2.Set(456)

    let status = hub.WaitCompletion()
    match status with
    | Status.SubscriptionError edi -> edi.SourceException.Message |> should equal "boom"
    | _ -> Assert.Fail()

    triggeredNever |> should equal false


[<Test>]
let subscribing_after_error_is_noop() =
    use hub = Hub.Create(1)

    let value1 = hub.GetSignal<int> "v1"
    let value2 = hub.GetSignal<int> "v2"

    let failCallback() =
        value1.Get<int>() |> ignore
        failwith "boom"

    hub.Subscribe "failSub" [ value1 ] failCallback
    value1.Set(1)

    match hub.WaitCompletion() with
    | Status.SubscriptionError _ -> ()
    | _ -> Assert.Fail()

    let triggered = new ManualResetEventSlim(false)
    let triggeredBg = new ManualResetEventSlim(false)

    hub.Subscribe "lateSub" [ value2 ] (fun () -> triggered.Set())
    hub.SubscribeBackground "lateBg" [ value2 ] (fun () -> triggeredBg.Set())

    value2.Set(2)

    triggered.Wait(100) |> should equal false
    triggeredBg.Wait(100) |> should equal false


[<Test>]
let reentrant_get_in_callback_does_not_deadlock() =
    use hub = Hub.Create(1)

    let signal = hub.GetSignal<int> "reentrant"

    let mutable triggered = false
    hub.Subscribe "reentrant-sub" [ signal ] (fun () ->
        signal.Get<int>() |> should equal 7
        triggered <- true)

    signal.Set(7)

    let waitTask = Task.Run(fun () -> hub.WaitCompletion())
    waitTask.Wait(500) |> should equal true

    waitTask.Result |> should equal Status.Ok
    triggered |> should equal true
