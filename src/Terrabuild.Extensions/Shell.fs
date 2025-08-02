namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Converters


/// <summary>
/// Provides support for running shell commands.
/// </summary>
type Shell() =

    /// <summary>
    /// Run a shell `command` using provided arguments.
    /// </summary>
    /// <param name="__dispatch__" example="echo">Example.</param>
    /// <param name="args" example="[ &quot;Hello Terrabuild&quot; ]">Arguments to pass to command.</param>
    static member __dispatch__ (context: ActionContext)
                               (args: string list option) =
        let args = args |> concat_quote
        let ops = [
            shellOp(context.Command, args)
        ]
        execRequest(Cacheability.Always, ops)
