module Terrabuild.Tests.Core.Auth

open System
open System.IO
open FsUnitTyped
open NUnit.Framework

let private withJobStore action =
    let root = Path.Combine(Path.GetTempPath(), $"terrabuild-auth-tests-{Guid.NewGuid():N}")
    Directory.CreateDirectory(root) |> ignore
    try action (CredentialStore.job root) root
    finally Directory.Delete(root, true)

let private staging = Uri("https://staging.example/")
let private production = Uri("https://production.example/")
let private workspaceAuth token masterKey : Auth.SpaceAuth =
    { Token = token; Key = Auth.deriveKey "workspace-a" masterKey }

[<Test>]
let ``workspace credentials are origin scoped and logout works without an origin`` () =
    withJobStore (fun store root ->
        let stagingAuth = workspaceAuth "staging-token" "staging-key"
        let productionAuth = workspaceAuth "production-token" "production-key"
        let other = workspaceAuth "other-token" "other-key"
        Auth.save store root staging "workspace-a" stagingAuth
        Auth.save store root staging "workspace-b" other
        Auth.read store production "workspace-a" |> shouldEqual None

        Auth.save store root production "workspace-a" productionAuth
        let nextProcess = CredentialStore.job root
        Auth.read nextProcess staging "workspace-a" |> shouldEqual (Some stagingAuth)
        Auth.read nextProcess production "workspace-a" |> shouldEqual (Some productionAuth)
        Auth.decodeKey stagingAuth.Key |> shouldEqual (Encryption.masterKeyFromString "workspace-a" "staging-key")
        if not (OperatingSystem.IsWindows()) then
            for file in Directory.EnumerateFiles(root) |> Seq.filter (fun path -> not (path.EndsWith(".lock"))) do
                File.GetUnixFileMode(file) |> shouldEqual (UnixFileMode.UserRead ||| UnixFileMode.UserWrite)

        Auth.remove nextProcess root "workspace-a"
        Auth.remove nextProcess root "workspace-a"
        Auth.read store staging "workspace-a" |> shouldEqual None
        Auth.read store production "workspace-a" |> shouldEqual None
        Auth.read store staging "workspace-b" |> shouldEqual (Some other))
