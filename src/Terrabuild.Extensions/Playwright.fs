namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Converters


/// <summary>
/// Runs Playwright end-to-end tests via `npx playwright`.
/// </summary>
type Playwright() =

    /// <summary>
    /// Executes `playwright test` with optional browser/project selection.
    /// </summary>
    /// <param name="browser" example="&quot;webkit&quot;">Browser to use.</param> 
    /// <param name="project" example="&quot;ci&quot;">Project to use.</param> 
    /// <param name="args" example="&quot;--debug&quot;">Additional arguments passed to `playwright test`.</param> 
    [<RemoteCacheAttribute>]
    static member test (browser: string option)
                       (project: string option)
                       (args: string option) =
        let browser = browser |> map_value (fun browser -> $"--browser {browser}")
        let project = project |> map_value (fun project -> $"--project {project}")
        let args = args |> or_default ""

        let ops = [
            shellOp("npx", $"playwright test {browser} {project} {args}")
        ]
        ops
