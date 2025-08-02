namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Converters


/// <summary>
/// Provides support for Playwright projects.
/// </summary>
type Playwright() =

    /// <summary>
    /// Run tests.
    /// </summary>
    /// <param name="browser" example="&quot;webkit&quot;">Browser to use.</param> 
    /// <param name="project" example="&quot;ci&quot;">Project to use.</param> 
    /// <param name="args" example="[ &quot;--debug&quot; ]">Arguments to pass to playwright.</param> 
    static member test (browser: string option)
                       (project: string option)
                       (args: string list option) =
        let browser = browser |> map_default (fun browser -> $"--browser {browser}")
        let project = project |> map_default (fun project -> $"--project {project}")
        let args = args |> concat_quote

        let ops = [
            shellOp("npx", $"playwright test {browser} {project} {args}")
        ]
        execRequest(Cacheability.Always, ops)
