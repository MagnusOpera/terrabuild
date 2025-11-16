namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Errors
open Converters

/// <summary>
/// Provides build, test, and exec helpers for projects that use **npm** and a `package.json` at the project root.
/// Infers dependencies from `package.json` to wire Terrabuild graphs and defaults outputs to `dist/**`.
/// </summary>
type Npm() =

    /// <summary>
    /// Infers project metadata from `package.json` (dependencies and default outputs).
    /// </summary>
    /// <param name="ignores" example="[ &quot;node_modules/**&quot; ]">Default ignore patterns used by Terrabuild.</param>
    /// <param name="outputs" example="[ &quot;dist/**&quot; ]">Default output globs produced by npm builds.</param>
    static member __defaults__(context: ExtensionContext) =
        try
            let projectFile = NpmHelpers.findProjectFile context.Directory
            let dependencies = projectFile |> NpmHelpers.findDependencies 
            let projectInfo = 
                { ProjectInfo.Default
                  with Outputs = Set [ "dist/**" ]
                       Dependencies = dependencies }
            projectInfo
        with
            exn -> forwardExternalError($"Error while processing project {context.Directory}", exn)

    /// <summary>
    /// Runs an arbitrary npm command (forwards the Terrabuild action name to `npm`).
    /// </summary>
    /// <param name="args" example="&quot;--port=1337&quot;">Additional arguments appended to the npm command.</param> 
    [<NoCacheAttribute>]
    static member __dispatch__ (context: ActionContext)
                               (args: string option) =
        let cmd = context.Command
        let args = args |> or_default ""

        let ops = [
            shellOp("npm", $"{cmd} {args}")
        ]
        ops


    /// <summary>
    /// Installs packages with `npm ci`, honoring the lock file.
    /// </summary>
    /// <param name="force" example="true">Adds `--force` to bypass failed checks.</param>
    /// <param name="clean" example="true">Enable clean install; set `true` to enforce `clean-install`.</param>
    /// <param name="args" example="&quot;--install-strategy hoisted&quot;">Additional arguments passed to `npm ci`.</param> 
    [<LocalCacheAttribute>]
    static member install (force: bool option)
                          (clean: bool option)
                          (args: string option) =
        let force = force |> map_true "--force"
        let clean = clean |> map_true "clean-"
        let args = args |> or_default ""

        let ops = [
            shellOp("npm", $"{clean}install {force} {args}")
        ]
        ops


    /// <summary>
    /// Runs the `build` script via `npm run build`.
    /// </summary>
    /// <param name="args" example="&quot;--workspaces&quot;">Extra arguments forwarded after `--`.</param> 
    [<RemoteCacheAttribute>]
    static member build (args: string option) =
        let args = args |> or_default ""
        let ops = [
            shellOp("npm", $"run build -- {args}")   
        ]
        ops


    /// <summary>
    /// Runs the `test` script via `npm run test`.
    /// </summary>
    /// <param name="args" example="&quot;--port=1337&quot;">Extra arguments forwarded after `--`.</param> 
    [<RemoteCacheAttribute>]
    static member test (args: string option) =
        let args = args |> or_default ""
        let ops = [
            shellOp("npm", $"run test -- {args}")   
        ]
        ops

    /// <summary>
    /// Runs an arbitrary npm script via `npm run &lt;target&gt;`.
    /// </summary>
    /// <param name="target" example="&quot;build-prod&quot;">Script target to invoke.</param> 
    /// <param name="args" example="&quot;build-prod&quot;">Extra arguments forwarded after `--`.</param> 
    [<LocalCacheAttribute>]
    static member run (target: string)
                      (args: string option) =
        let args = args |> or_default ""
        let ops = [
            shellOp("npm", $"run {target} -- {args}")
        ]
        ops

    /// <summary>
    /// Executes a package binary via `npm exec`.
    /// </summary>
    /// <param name="package" example="&quot;hello-world-npm&quot;">Package to run with `npm exec`.</param> 
    /// <param name="args" example="&quot;build-prod&quot;">Arguments passed to the package command.</param> 
    [<LocalCacheAttribute>]
    static member exec (package: string)
                       (args: string option) =
        let args = args |> or_default ""
        let ops = [
            shellOp("npm", $"exec -- {package} {args}")
        ]
        ops
