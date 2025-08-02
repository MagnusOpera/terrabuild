namespace Terrabuild.Extensions
open Terrabuild.Extensibility


/// <summary>
/// Provides support for Playwright projects.
/// </summary>
type Playwright() =

    /// <summary>
    /// Run tests.
    /// </summary>
    /// <param name="args" example="[ &quot;--debug&quot; ]">Arguments to pass to npx.</param> 
    static member test (context: ActionContext) (browser: string option) (project: string option) (args: string list option) =
        let browser =
            match browser with
            | Some browser -> $"--browser {browser}"
            | _ -> ""

        let project =
            match project with
            | Some project -> $"--project {project}"
            | _ -> ""

        let args = args |> Option.map (String.join " ") |> Option.defaultValue ""

        let ops = [
            shellOp("npx", $"playwright test {browser} {project} {args}")
        ]
        execRequest(Cacheability.Always, ops)
