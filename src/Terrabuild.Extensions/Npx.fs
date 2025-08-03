namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Converters


/// <summary>
/// Provides support for running npx commands.
/// </summary>
type Npx() =

    /// <summary>
    /// Run an npx command.
    /// </summary>
    /// <param name="package" example="&quot;hello-world-npm&quot;">Package to exec.</param> 
    /// <param name="args" example="&quot;&quot;">Arguments to pass to npx.</param> 
    static member run (package: string)
                      (args: string option) =
        let args = args |> or_default ""
        let ops = [
            shellOp("npx", $"--yes -- {package} {args}")
        ]
        ops |> execRequest Cacheability.Local
