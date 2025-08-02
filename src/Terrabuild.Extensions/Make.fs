namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Converters

/// <summary>
/// Add support for Makefile.
/// </summary>
type Make() =

    /// <summary>
    /// Invoke make target.
    /// </summary>
    /// <param name="variables" example="{ configuration: &quot;Release&quot; }">Variables to pass to make target.</param>
    /// <param name="args" example="[ &quot;-d&quot; ]">Arguments for command.</param>
    static member __dispatch__ (context: ActionContext)
                               (variables: Map<string, string> option)
                               (args: string list option) =
        let variables = variables |> format_space (fun kvp -> $"{kvp.Key}=\"{kvp.Value}\"")
        let args = args |> concat_quote

        let ops = [ shellOp("make", $"{context.Command} {variables} {args}") ]
        execRequest(Cacheability.Always, ops)
