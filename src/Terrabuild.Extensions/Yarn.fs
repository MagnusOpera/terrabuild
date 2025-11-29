namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Converters
open Errors


/// <summary>
/// Provides build/test helpers for projects using **yarn** and a `package.json`.
/// Defaults outputs to `dist/**` and infers dependencies from `package.json` to link Terrabuild graphs.
/// </summary>
type Yarn() =

    /// <summary>
    /// Infers project metadata from `package.json` (dependencies and default outputs).
    /// </summary>
    /// <param name="ignores" example="[ &quot;node_modules/**&quot; ]">Default ignore patterns used by Terrabuild.</param>
    /// <param name="outputs" example="[ &quot;dist/**&quot; ]">Default outputs produced by yarn builds.</param>
    static member __defaults__(context: ExtensionContext) =
        try
            let packageFile = NpmHelpers.findProjectFile context.Directory
            let package = NpmHelpers.loadPackage packageFile
            let dependencies = package |> NpmHelpers.findLocalPackages 
            let projectInfo = 
                { ProjectInfo.Default
                  with Outputs = Set [ "dist/**" ]
                       Dependencies = dependencies }
            projectInfo
        with
            exn -> forwardExternalError($"Error while processing project {context.Directory}", exn)


    /// <summary>
    /// Runs an arbitrary yarn command (Terrabuild action name is forwarded to `yarn`).
    /// </summary>
    /// <param name="args" example="&quot;--port=1337&quot;">Additional arguments appended after the yarn command.</param> 
    [<NoCacheAttribute>]
    static member __dispatch__ (context: ActionContext)
                               (args: string option) =
        let args = args |> or_default ""

        let ops = [
            shellOp("yarn", $"{context.Command} -- {args}")   
        ]
        ops


    /// <summary>
    /// Installs packages with `yarn install`, optionally updating the lockfile or ignoring engines.
    /// </summary>
    /// <param name="update" example="true">Allow lockfile updates (omit to enforce frozen lockfile).</param> 
    /// <param name="ignore-engines" example="true">Adds `--ignore-engines`.</param> 
    /// <param name="args" example="&quot;--verbose&quot;">Additional arguments for `yarn install`.</param> 
    [<LocalCacheAttribute>]
    static member install (update: bool option)
                          (``ignore-engines``: bool option)
                          (args: string option) =
        let update = update |> map_false "--frozen-lockfile"
        let ignoreEngines = ``ignore-engines`` |> map_true "--ignore-engines"
        let args = args |> or_default ""

        let ops = [ shellOp("yarn", $"install {update} {ignoreEngines} {args}") ]
        ops


    /// <summary>
    /// Runs the `build` script via `yarn build`.
    /// </summary>
    /// <param name="args" example="&quot;--verbose&quot;">Additional arguments forwarded after `--`.</param> 
    [<RemoteCacheAttribute>]
    static member build (args: string option) =
        let args = args |> or_default ""

        let ops = [
            shellOp("yarn", $"build -- {args}")
        ]
        ops


    /// <summary>
    /// Runs the `test` script via `yarn test`.
    /// </summary>
    /// <param name="args" example="&quot;--verbose&quot;">Additional arguments forwarded after `--`.</param> 
    [<RemoteCacheAttribute>]
    static member test (args: string option) =
        let args = args |> or_default ""

        let ops = [
            shellOp("yarn", $"test -- {args}")
        ]
        ops

    /// <summary>
    /// Runs an arbitrary yarn script (`yarn &lt;command&gt;`).
    /// </summary>
    /// <param name="command" example="&quot;build-prod&quot;">Command to run.</param> 
    /// <param name="args" example="&quot;build-prod&quot;">Additional arguments forwarded after `--`.</param> 
    [<LocalCacheAttribute>]
    static member run (command: string)
                      (args: string option) =
        let args = args |> or_default ""

        let ops = [
            shellOp("yarn", $"{command} -- {args}")
        ]
        ops
