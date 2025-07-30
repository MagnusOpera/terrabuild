module Terrabuild.PubSub.Tests

open NUnit.Framework
open FsUnit


[<Test>]
let successful() =
    let hub = Hub.Create(1)

    let value1 = hub.GetSignal<int> "computed1"
    let computed2 = hub.GetSignal<string> "computed2"

    let computed1 = hub.GetSignal<int> "computed1"
    let value2 = hub.GetSignal<string> "computed2"

    let mutable triggered0 = false
    let callback0() =
        triggered0 <- true

    let mutable triggered1 = false
    let callback1() =
        value1.Value |> should equal 42
        computed2.Value <- "tralala"
        triggered1 <- true

    let mutable triggered2 = false
    let mutable triggered3 = false
    let callback2() =
        value1.Value |> should equal 42
        value2.Value |> should equal "tralala"
        triggered2 <- true

        // callback3 must be immediately triggered as computed1/2 are immediately available
        let callback3() =
            // getting another computed lead to same value
            value1.Value |> should equal 42
            value2.Value |> should equal "tralala"
            hub.GetSignal<int>("computed1").Value |> should equal 42
            triggered3 <- true

        hub.SubscribeTask "subscription3" [ value1; value2 ] callback3


    hub.SubscribeTask "callback0" [] callback0
    hub.SubscribeTask "callback1" [ value1 ] callback1
    hub.SubscribeTask "callback2" [ value1; value2 ] callback2

    computed1.Value <- 42

    let status = hub.WaitCompletion()

    status |> should equal Status.Ok
    value1.Value |> should equal 42
    value2.Value |> should equal "tralala"
    triggered0 |> should equal true
    triggered1 |> should equal true
    triggered2 |> should equal true
    triggered3 |> should equal true


[<Test>]
let exception_in_callback_is_error() =
    let hub = Hub.Create(1)

    let value1 = hub.GetSignal<int> "computed1"
    let computed2 = hub.GetSignal<string> "computed2"
    let value3 = hub.GetSignal<float> "computed3"

    let computed1 = hub.GetSignal<int> "computed1"
    let value2 = hub.GetSignal<string> "computed2"
    let computed3 = hub.GetSignal<float> "computed3"

    let mutable triggered1 = false
    let callback() =
        value1.Value |> should equal 42
        value2.Value |> should equal "tralala"
        triggered1 <- true
        failwith "workflow failed"

    let mutable triggered2 = false
    let neverCallback() =
        triggered2 <- true
        failwith "Callback shall never be called"

    hub.SubscribeTask "subscription1" [ value1; value2 ] callback
    hub.SubscribeTask "subscription2" [ value3 ] neverCallback

    computed1.Value <- 42
    computed2.Value <- "tralala"

    // callback fails
    let status = hub.WaitCompletion()

    match status with
    | Status.SubscriptionError exn -> exn.Message |> should equal "workflow failed"
    | _ -> Assert.Fail()
    value1.Value |> should equal 42
    value2.Value |> should equal "tralala"
    triggered1 |> should equal true
    triggered2 |> should equal false


[<Test>]
let unsignaled_subscription1_is_error() =
    let hub = Hub.Create(1)

    let value1 = hub.GetSignal<int> "computed1"
    let computed2 = hub.GetSignal<string> "computed2"
    let value3 = hub.GetSignal<float> "computed3"

    let computed1 = hub.GetSignal<int> "computed1"
    let value2 = hub.GetSignal<string> "computed2"
    let computed3 = hub.GetSignal<float> "computed3"

    let mutable triggered1 = false
    let callback() =
        value1.Value |> should equal 42
        value2.Value |> should equal "tralala"
        triggered1 <- true

    let mutable triggered2 = false
    let neverCallback() =
        triggered2 <- true
        failwith "Callback shall never be called"

    hub.SubscribeTask "subscription1" [ value1; value2 ] callback
    hub.SubscribeTask "subscription2" [ value3 ] neverCallback

    computed1.Value <- 42
    computed2.Value <- "tralala"

    // computed3 is never triggered
    let status = hub.WaitCompletion()

    match status with
    | Status.UnfulfilledSubscription (subscription, signals) ->
        subscription |> should equal "subscription2"
        signals |> should equal (Set ["computed3"])
    | _ -> Assert.Fail()
    triggered1 |> should equal true
    triggered2 |> should equal false
    value1.Value |> should equal 42
    value2.Value |> should equal "tralala"
    (fun () -> value3.Value |> ignore) |> should throw typeof<Errors.TerrabuildException>


[<Test>]
let unsignaled_subscription2_is_error() =
    let hub = Hub.Create(1)

    let value1 = hub.GetSignal<int> "computed1"
    let computed2 = hub.GetSignal<string> "computed2"
    let value3 = hub.GetSignal<float> "computed3"

    let computed1 = hub.GetSignal<int> "computed1"
    let value2 = hub.GetSignal<string> "computed2"
    let computed3 = hub.GetSignal<float> "computed3"

    let mutable triggered1 = false
    let callback() =
        value1.Value |> should equal 42
        triggered1 <- true

    let mutable triggered2 = false
    let neverCallback() =
        triggered2 <- true
        failwith "Callback shall never be called"

    hub.SubscribeTask "subscription1" [ value1 ] callback
    hub.SubscribeTask "subscription2" [ value2; value3 ] neverCallback

    computed1.Value <- 42

    // computed3 is never triggered
    let status = hub.WaitCompletion()

    match status with
    | Status.UnfulfilledSubscription (subscription, signals) ->
        subscription |> should equal "subscription2"
        signals |> should equal (Set ["computed2"; "computed3"])
    | _ -> Assert.Fail()
    triggered1 |> should equal true
    triggered2 |> should equal false
    value1.Value |> should equal 42
    (fun () -> value3.Value |> ignore) |> should throw typeof<Errors.TerrabuildException>



[<Test>]
let computed_must_match_type() =
    let hub = Hub.Create(1)

    let value1 = hub.GetSignal<int> "computed1"
    (fun () -> hub.GetSignal<string> "computed1" |> ignore) |> should throw typeof<Errors.TerrabuildException>

    let computed2 = hub.GetSignal<string> "computed2"
    (fun () -> hub.GetSignal<int> "computed2" |> ignore) |> should throw typeof<Errors.TerrabuildException>



[<Test>]
let download_subscription_priority() =
    let hub = Hub.Create(2)
    let value1 = hub.GetSignal<int> "download1"
    let value2 = hub.GetSignal<int> "download2"
    let mutable triggered = false
    let callback() =
        value1.Value |> should equal 99
        value2.Value |> should equal 100
        triggered <- true
    hub.SubscribeDownload "downloadSub" [ value1; value2 ] callback
    value1.Value <- 99
    value2.Value <- 100
    let status = hub.WaitCompletion()
    status |> should equal Status.Ok
    triggered |> should equal true
