namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Converters


/// <summary>
/// Executes generic shell commands from Terrabuild actions (for simple scripting hooks).
/// </summary>
type Shell() =

    /// <summary>
    /// Runs the Terrabuild action name as the shell command.
    /// </summary>
    /// <param name="args" example="&quot;Hello Terrabuild&quot;">Arguments passed to the command.</param>
    [<NoCacheAttribute>]
    static member __dispatch__ (context: ActionContext)
                               (args: string option) =
        let args = args |> or_default ""

        let ops = [
            shellOp(context.Command, args)
        ]
        ops
