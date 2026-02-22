module Terrabuild.Scripting.Invocable.Tests

open NUnit.Framework
open FsUnit

open Terrabuild.Scripting
open Terrabuild.Expression


type Arguments = {
    Name: string
    Address: string option
}

type Scripting() =
    static member Hello (name: string) =
        $"Hello {name}"

    static member HelloOption (name: string option) =
        let name = name |> Option.defaultValue "None"
        $"Hello {name} !"

    static member HelloMap (names: Map<string, string>) =
        let values = names |> Map.map (fun k v -> $"{k} {v}")
        values.Values |> String.join " "

    static member HelloArgs (args: Arguments) =
        $"Hello {args.Name} !"

    static member HelloMapOption (names: Map<string, string> option) =
        match names with
        | Some names -> Scripting.HelloMap names
        | None -> "None"

    static member HelloList (names: string list) =
        names |> String.join " "

    static member HelloListOption (names: string list option) =
        match names with
        | Some names -> Scripting.HelloList names
        | None -> "None"

let getMethod name =
    let method = typeof<Scripting>.GetMethod(name) |> nonNull
    method

[<Test>]
let invokeScalar() =
    let method = getMethod "Hello"
    let invocable = Invocable(method)

    let args = Value.Map (Map ["name", Value.String "Pierre"])
    let result = invocable.Invoke<string> args
    result |> should equal "Hello Pierre"


[<Test>]
let invokeOptionNone() =
    let method = getMethod "HelloOption"
    let invocable = Invocable(method)

    let args = Value.Map Map.empty
    let result = invocable.Invoke<string> args
    result |> should equal "Hello None !"

[<Test>]
let invokeOptionSome() =
    let method = getMethod "HelloOption"
    let invocable = Invocable(method)

    let args = Value.Map (Map ["name", Value.String "Pierre"])
    let result = invocable.Invoke<string> args
    result |> should equal "Hello Pierre !"

[<Test>]
let invokeRecord() =
    let method = getMethod "HelloArgs"
    let invocable = Invocable(method)
    
    let args = Value.Map (Map ["args", Value.Map (Map ["Name", Value.String "Pierre"])])
    let result = invocable.Invoke<string> args
    result |> should equal "Hello Pierre !"


[<Test>]
let invokeMap() =
    let method = getMethod "HelloMap"
    let invocable = Invocable(method)

    let names = Value.Map (Map ["Hello", Value.String "Pierre"])
    let args = Value.Map (Map ["names", names])    
    let result = invocable.Invoke<string> args
    result |> should equal "Hello Pierre"

[<Test>]
let invokeMapNone() =
    let method = getMethod "HelloMapOption"
    let invocable = Invocable(method)

    let args = Value.Map (Map.empty)
    let result = invocable.Invoke<string> args
    result |> should equal "None"

[<Test>]
let invokeMapSome() =
    let method = getMethod "HelloMapOption"
    let invocable = Invocable(method)

    let names = Value.Map (Map ["Hello", Value.String "Pierre"])
    let args = Value.Map (Map ["names", names])    
    let result = invocable.Invoke<string> args
    result |> should equal "Hello Pierre"



[<Test>]
let invokeList() =
    let method = getMethod "HelloList"
    let invocable = Invocable(method)

    let names = Value.List [Value.String "Hello"; Value.String "Pierre"]
    let args = Value.Map (Map ["names", names])
    let result = invocable.Invoke<string> args
    result |> should equal "Hello Pierre"

[<Test>]
let invokeListNone() =
    let method = getMethod "HelloListOption"
    let invocable = Invocable(method)

    let args = Value.Map (Map.empty)
    let result = invocable.Invoke<string> args
    result |> should equal "None"

[<Test>]
let invokeListSome() =
    let method = getMethod "HelloListOption"
    let invocable = Invocable(method)

    let names = Value.List [Value.String "Hello"; Value.String "Pierre"]
    let args = Value.Map (Map ["names", names])
    let result = invocable.Invoke<string> args
    result |> should equal "Hello Pierre"
