module Tests.Environment
open FsUnit
open NUnit.Framework

let getVarName prefix =
    let rnd = System.Random()
    let name = $"tagada{rnd.Next()}"
    name, $"{prefix}{name}"

[<Test>]
let ``no variable returns None``() =
    let name, _ = getVarName "TB_VAR"
    name
    |> Environment.getTerrabuildEnvVar
    |> should equal None

[<Test>]
let ``not matching prefix returns None``() =
    let name, tbvar = getVarName "TB_var_"

    System.Environment.SetEnvironmentVariable(tbvar, "pouet pouet")
    name
    |> Environment.getTerrabuildEnvVar
    |> should equal None
    System.Environment.SetEnvironmentVariable(tbvar, null)

[<Test>]
let ``matching case returns Some``() =
    let name, tbvar = getVarName "TB_VAR_"

    System.Environment.SetEnvironmentVariable(tbvar, "pouet pouet")
    name
    |> Environment.getTerrabuildEnvVar
    |> should equal (Some "pouet pouet")
    System.Environment.SetEnvironmentVariable(tbvar, null)

[<Test>]
let ``not matching case returns None``() =
    let name, tbvar = getVarName "TB_VAR_"
    let name = name |> String.toUpper

    System.Environment.SetEnvironmentVariable(tbvar, "pouet pouet")
    name
    |> Environment.getTerrabuildEnvVar
    |> should equal None
    System.Environment.SetEnvironmentVariable(tbvar, null)
