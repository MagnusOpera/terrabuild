module Tests.Environment
open FsUnit
open NUnit.Framework

let getVarName() =
    let rnd = System.Random()
    $"tagada{rnd.Next()}"

[<Test>]
let ``no variable returns None``() =
    let name = getVarName()

    name
    |> Environment.getTerrabuildEnvVar
    |> should equal None

[<Test>]
let ``not matching prefix returns None``() =
    let name = getVarName()
    System.Environment.SetEnvironmentVariable($"TB_var_{name}", "pouet pouet")

    name
    |> Environment.getTerrabuildEnvVar
    |> should equal None

    System.Environment.SetEnvironmentVariable($"TB_var_{name}", null)

[<Test>]
let ``matching case returns Some``() =
    let name = getVarName()
    System.Environment.SetEnvironmentVariable($"TB_VAR_{name}", "pouet pouet")

    name
    |> Environment.getTerrabuildEnvVar
    |> should equal (Some "pouet pouet")

    System.Environment.SetEnvironmentVariable($"TB_var_{name}", null)

[<Test>]
let ``not matching case returns Some``() =
    let name = getVarName() |> String.toUpper
    System.Environment.SetEnvironmentVariable($"TB_VAR_{name}", "pouet pouet")

    name
    |> Environment.getTerrabuildEnvVar
    |> should equal (Some "pouet pouet")

    System.Environment.SetEnvironmentVariable($"TB_var_{name}", null)
