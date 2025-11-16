namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Converters


/// <summary>
/// Runs transient npm packages with **npx** (auto-accepts prompts with `--yes`).
/// </summary>
type Npx() =

    /// <summary>
    /// Executes a package binary via `npx --yes`.
    /// </summary>
    /// <param name="package" example="&quot;hello-world-npm&quot;">Package to execute.</param> 
    /// <param name="args" example="&quot;&quot;">Arguments forwarded to the package.</param> 
    [<LocalCacheAttribute>]
    static member run (package: string)
                      (args: string option) =
        let args = args |> or_default ""
        let ops = [
            shellOp("npx", $"--yes -- {package} {args}")
        ]
        ops
