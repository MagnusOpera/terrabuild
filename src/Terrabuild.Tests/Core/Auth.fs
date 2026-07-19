module Terrabuild.Tests.Core.Auth
open System
open System.IO
open FsUnit
open NUnit.Framework

let private withTempHome action =
    let root = Path.Combine(Path.GetTempPath(), $"terrabuild-auth-tests-{Guid.NewGuid():N}")
    let previousHome = Environment.GetEnvironmentVariable("HOME")
    Directory.CreateDirectory(root) |> ignore
    Environment.SetEnvironmentVariable("HOME", root)
    try
        action root
    finally
        Environment.SetEnvironmentVariable("HOME", previousHome)
        if Directory.Exists(root) then
            Directory.Delete(root, true)

let private workspaceAuth id token masterKey : Auth.SpaceAuth =
    { Id = id
      Token = token
      MasterKey = masterKey }

let private writeConfiguration root spaceAuths =
    let profile = Path.Combine(root, ".terrabuild")
    Directory.CreateDirectory(profile) |> ignore
    { Auth.Configuration.SpaceAuths = spaceAuths }
    |> Json.Serialize
    |> fun config -> File.WriteAllText(Path.Combine(profile, "config.json"), config)

[<Test>]
let ``logout removes only the selected workspace credentials`` () =
    withTempHome (fun root ->
        let selected = workspaceAuth "workspace-a" "token-a" "master-key-a"
        let other = workspaceAuth "workspace-b" "token-b" "master-key-b"
        writeConfiguration root [ selected; other ]

        Auth.logout selected.Id

        Auth.readAuth selected.Id |> should equal None
        Auth.readAuth other.Id |> should equal (Some other))

[<Test>]
let ``logout is safe when no authentication configuration exists`` () =
    withTempHome (fun _ ->
        Auth.logout "missing-workspace"

        Auth.readAuth "missing-workspace" |> should equal None)
