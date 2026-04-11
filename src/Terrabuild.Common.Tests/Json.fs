module Tests.Json
open System.Text.Json
open FsUnit
open NUnit.Framework

type BuildState =
    | Pending
    | Succeeded of string
    | Failed of string * int

type BuildInfo =
    { Name: string
      Note: string option
      State: BuildState }

let private getRequiredString (element: JsonElement) =
    match element.GetString() |> Option.ofObj with
    | None -> failwith "Expected JSON string value"
    | Some value -> value

let private serialize (value: objnull) =
    Json.Serialize (Unchecked.nonNull value)

[<Test>]
let ``serialize and deserialize discriminated unions``() =
    let json = box (Failed ("lint", 2)) |> serialize

    use doc = JsonDocument.Parse(json)
    let values = doc.RootElement.EnumerateArray() |> Seq.toArray

    values.Length |> should equal 3
    values[0] |> getRequiredString |> should equal "failed"
    values[1] |> getRequiredString |> should equal "lint"
    values[2].GetInt32() |> should equal 2

    json |> Json.Deserialize<BuildState> |> should equal (Failed ("lint", 2))

[<Test>]
let ``unwrap option values at the root``() =
    box (Some "ready" : string option) |> serialize |> should equal "\"ready\""
    box (None : string option) |> serialize |> should equal "null"

    "\"ready\"" |> Json.Deserialize<string option> |> should equal (Some "ready")

[<Test>]
let ``skip none option fields on records``() =
    let json =
        box
            { Name = "build"
              Note = None
              State = Pending }
        |> serialize

    use doc = JsonDocument.Parse(json)
    let root = doc.RootElement
    let propertyNames = root.EnumerateObject() |> Seq.map (fun property -> property.Name) |> Set.ofSeq

    propertyNames |> should equal (Set.ofList [ "name"; "state" ])
    root.GetProperty("state") |> getRequiredString |> should equal "pending"

    json
    |> Json.Deserialize<BuildInfo>
    |> should equal
        { Name = "build"
          Note = None
          State = Pending }
