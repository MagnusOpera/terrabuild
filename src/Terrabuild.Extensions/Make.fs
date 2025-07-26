namespace Terrabuild.Extensions

open Terrabuild.Extensibility

/// <summary>
/// Add support for Makefile.
/// </summary>
type Make() =

    /// <summary>
    /// Invoke make target.
    /// </summary>
    /// <param name="variables" example="{ configuration: &quot;Release&quot; }">Variables to pass to make target.</param>
    static member __dispatch__ (context: ActionContext) (variables: Map<string, string>) =
        let args = variables |> Seq.fold (fun acc kvp -> $"{acc} {kvp.Key}=\"{kvp.Value}\"") $"{context.Command}"
        let ops = [ shellOp("make", args) ]
        execRequest(Cacheability.Always, ops)
