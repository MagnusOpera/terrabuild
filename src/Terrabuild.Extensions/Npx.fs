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
    /// <param name="args" example="[ ]">Arguments to pass to npx.</param> 
    static member run (package: string)
                      (args: string list option) =
        let args = args |> concat_quote

        let ops = [
            shellOp("npx", $"--yes -- {package} {args}")
        ]
        ops |> execRequest Cacheability.Local
