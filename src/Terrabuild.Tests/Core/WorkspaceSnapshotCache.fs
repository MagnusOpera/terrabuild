module Terrabuild.Tests.Core.WorkspaceSnapshotCache

open System
open System.IO
open System.Threading
open NUnit.Framework

let private withCache lifetime action =
    let root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(root) |> ignore
    try
        use cache = new WorkspaceSnapshotCache.Cache<string, int>(root, lifetime)
        action root cache
    finally
        Directory.Delete(root, true)

[<Test>]
let ``snapshots reuse matching keys and refresh after invalidation`` () =
    withCache (TimeSpan.FromMinutes(1.0)) (fun _ cache ->
        let mutable loads = 0
        let load () = loads <- loads + 1; loads
        Assert.That(cache.Get("default", load), Is.EqualTo(1))
        Assert.That(cache.Get("default", load), Is.EqualTo(1))
        Assert.That(cache.Get("other", load), Is.EqualTo(2))
        cache.Invalidate()
        Assert.That(cache.Get("default", load), Is.EqualTo(3)))

[<Test>]
let ``changes during loading and failures do not poison snapshots`` () =
    withCache (TimeSpan.FromMinutes(1.0)) (fun _ cache ->
        Assert.Throws<InvalidOperationException>(fun () -> cache.Get("default", fun () -> invalidOp "failed") |> ignore) |> ignore
        cache.Get("default", fun () -> cache.Invalidate(); 1) |> ignore
        Assert.That(cache.Get("default", fun () -> 2), Is.EqualTo(2)))

[<Test>]
let ``expiry refreshes inputs outside the workspace`` () =
    withCache TimeSpan.Zero (fun _ cache ->
        cache.Get("default", fun () -> 1) |> ignore
        Assert.That(cache.Get("default", fun () -> 2), Is.EqualTo(2)))

[<Test>]
let ``workspace file changes invalidate snapshots`` () =
    withCache (TimeSpan.FromMinutes(1.0)) (fun root cache ->
        let mutable loads = 0
        let load () = loads <- loads + 1; loads
        cache.Get("default", load) |> ignore
        File.WriteAllText(Path.Combine(root, "PROJECT"), "changed")
        Assert.That(SpinWait.SpinUntil((fun () -> cache.Get("default", load) > 1), 5000), Is.True))
