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
    /// <param name="args" example="[ &quot;hello-world-npm&quot; ]">Arguments to pass to npx.</param> 
    static member run (args: string list option) =
        let args = args |> concat_quote

        let ops = [
            shellOp("npx", $"--yes {args}")
        ]
        ops |> execRequest Cacheability.Always
