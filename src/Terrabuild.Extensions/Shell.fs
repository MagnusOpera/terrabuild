namespace Terrabuild.Extensions
open Terrabuild.Extensibility


/// <summary>
/// Provides support for running shell commands.
/// </summary>
type Shell() =

    /// <summary>
    /// Run a shell `command` using provided arguments.
    /// </summary>
    /// <param name="__dispatch__" example="echo">Example.</param>
    /// <param name="arguments" example="&quot;Hello Terrabuild&quot;">Arguments to pass to command.</param>
    static member __dispatch__ (context: ActionContext) (arguments: string option) =
        let arguments = arguments |> Option.defaultValue ""
        let ops = [ shellOp(context.Command, arguments) ]
        execRequest(Cacheability.Always, ops)
