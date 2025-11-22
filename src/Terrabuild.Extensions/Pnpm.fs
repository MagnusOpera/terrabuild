namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Errors
open Converters

/// <summary>
/// Provides build/install/test helpers for projects using **pnpm** and a `package.json`.
/// Supports batch (cascade) builds when `pnpm-workspace.yaml` and a root `package.json` exist at the Terrabuild workspace root.
/// </summary>
type Pnpm() =

    /// <summary>
    /// Infers project metadata from `package.json` (dependencies and default outputs).
    /// </summary>
    /// <param name="ignores" example="[ &quot;node_modules/**&quot; ]">Default ignore patterns used by Terrabuild.</param>
    /// <param name="outputs" example="[ &quot;dist/**&quot; ]">Default outputs produced by pnpm builds.</param>
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
    /// Runs an arbitrary pnpm command (Terrabuild action name is forwarded to `pnpm`).
    /// </summary>
    /// <param name="args" example="&quot;--port=1337&quot;">Additional arguments appended after the pnpm command.</param> 
    [<NoCacheAttribute>]
    static member __dispatch__ (context: ActionContext)
                               (args: string option) =
        let cmd = context.Command
        let args = args |> or_default ""

        let ops = [
            shellOp("pnpm", $"{cmd} {args}")
        ]
        ops


    /// <summary>
    /// Installs packages with `pnpm install`, optionally honoring the lockfile and batching across workspaces.
    /// </summary>
    /// <param name="force" example="true">Adds `--force` to reinstall when checks fail.</param> 
    /// <param name="frozen" example="true">Enable frozen versions; set `true` to enforce `--frozen-lockfile`.</param>
    /// <param name="args" example="&quot;--no-color&quot;">Additional arguments for `pnpm install`.</param> 
    [<LocalCacheAttribute>]
    [<BatchableAttribute>]
    static member install (context: ActionContext)
                          (force: bool option)
                          (frozen: bool option)
                          (args: string option) =
        let force = force |> map_true "--force"
        let frozen = frozen |> map_true "--frozen-lockfile"
        let args = args |> or_default ""
        let filters =
            match context.Batch with
            | Some batch -> batch.ProjectPaths |> List.fold (fun acc project -> $"{acc} --filter ./{project}") "--recursive"
            | _ -> ""

        let ops = [
            shellOp("pnpm", $"{filters} install {frozen} --link-workspace-packages {force} {args}")
        ]
        ops


    /// <summary>
    /// Runs the `build` script (`pnpm run build`) across targeted workspaces.
    /// </summary>
    /// <param name="args" example="&quot;--no-color&quot;">Additional arguments for the script.</param> 
    [<RemoteCacheAttribute>]
    [<BatchableAttribute>]
    static member build (context: ActionContext)
                        (args: string option) =
        let args = args |> or_default ""
        let filters =
            match context.Batch with
            | Some batch -> batch.ProjectPaths |> List.fold (fun acc project -> $"{acc} --filter ./{project}") "--recursive"
            | _ -> ""
        let ops = [
            shellOp("pnpm", $"{filters} run build {args}")
        ]
        ops


    /// <summary>
    /// Runs the `test` script (`pnpm run test`) across targeted workspaces.
    /// </summary>
    /// <param name="args" example="&quot;--port=1337&quot;">Additional arguments for the script.</param> 
    [<RemoteCacheAttribute>]
    [<BatchableAttribute>]
    static member test (context: ActionContext)
                       (args: string option) =
        let args = args |> or_default ""
        let filters =
            match context.Batch with
            | Some batch -> batch.ProjectPaths |> List.fold (fun acc project -> $"{acc} --filter ./{project}") "--recursive"
            | _ -> ""
        let ops = [
            shellOp("pnpm", $"{filters} run test {args}")
        ]
        ops

    /// <summary>
    /// Runs an arbitrary pnpm script (`pnpm run &lt;target&gt;`).
    /// </summary>
    /// <param name="target" example="&quot;build-prod&quot;">Target to invoke.</param> 
    /// <param name="args" example="&quot;build-prod&quot;">Additional arguments forwarded to the script.</param>
    /// <param name="no_recursive" example="true">Skip `--recursive` when targeting a single workspace.</param>
    [<LocalCacheAttribute>]
    static member run (target: string)
                      (no_recursive: bool option)
                      (args: string option) =
        let args = args |> or_default ""
        let recursive = no_recursive |> map_false "--recursive"
        let ops = [
            shellOp("pnpm", $"{recursive} run {target} {args}")
        ]
        ops
