module Terrabuild.Tests.RepositorySourceControls

open FsUnit
open NUnit.Framework

[<TestCase("git@github.com:MagnusOpera/terrabuild.git", "magnusopera/terrabuild")>]
[<TestCase("https://github.com/MagnusOpera/terrabuild.git", "magnusopera/terrabuild")>]
[<TestCase("ssh://git@github.com/MagnusOpera/terrabuild.git", "magnusopera/terrabuild")>]
[<TestCase("https://gitlab.example.com/group/subgroup/repo.git", "gitlab.example.com/group/subgroup/repo")>]
let ``normalize repository identity`` repository expected =
    Git.tryNormalizeRepositoryIdentity repository |> should equal (Some expected)
