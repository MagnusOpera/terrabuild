namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Converters

/// <summary>
/// Invokes GNU Make targets, passing variables and extra arguments through Terrabuild.
/// Assumes a `Makefile` in the project directory.
/// </summary>
type Make() =

    /// <summary>
    /// Invokes the Terrabuild action name as the make target.
    /// </summary>
    /// <param name="variables" example="{ configuration: &quot;Release&quot; }">`KEY=VALUE` variables injected before the target.</param>
    /// <param name="args" example="&quot;-d&quot;">Additional arguments passed to `make`.</param>
    [<NoCacheAttribute>]
    static member __dispatch__ (context: ActionContext)
                               (variables: Map<string, string> option)
                               (args: string option) =
        let variables = variables |> format_space (fun kvp -> $"{kvp.Key}=\"{kvp.Value}\"")
        let args = args |> or_default ""

        let ops = [
            shellOp("make", $"{context.Command} {variables} {args}")
        ]
        ops
