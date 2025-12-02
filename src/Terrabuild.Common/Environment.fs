module Environment
open System
open System.Collections
open System.Text.RegularExpressions

let envVar (varName: string) =
    varName |> Environment.GetEnvironmentVariable |> Option.ofObj

let currentDir() = System.Environment.CurrentDirectory

let envVars() =
    Environment.GetEnvironmentVariables()
    |> Seq.cast<DictionaryEntry>
    |> Seq.map (fun entry -> $"{entry.Key}", $"{entry.Value}")
    |> Map.ofSeq

let expandTerrabuildHome (terrabuildHome: string) (input: string) : string =
    // Match either $TERRABUILD_HOME or ${TERRABUILD_HOME} not followed by a letter/underscore/digit
    let pattern = @"(?<!\w)\$(TERRABUILD_HOME)(?![\w])|\$\{TERRABUILD_HOME\}"
    Regex.Replace(input, pattern, terrabuildHome)

let getTerrabuildEnvVar name =
    match Environment.GetEnvironmentVariable($"TB_VAR_{name}") with
    | null -> None
    | value -> Some value
