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
    /// <param name="args" example="&quot;Hello Terrabuild&quot;">Arguments to pass to command.</param>
    [<NoCacheAttribute>]
    static member __dispatch__ (context: ActionContext)
                               (args: string option) =
        let args = args |> or_default ""

        let ops = [
            shellOp(context.Command, args)
        ]
        ops
