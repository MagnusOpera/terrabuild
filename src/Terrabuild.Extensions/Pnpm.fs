namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Errors
open Converters

/// <summary>
/// Provides support for `pnpm`.
/// </summary>
type Pnpm() =

    /// <summary>
    /// Provides default values.
    /// </summary>
    /// <param name="ignores" example="[ &quot;node_modules/**&quot; ]">Default values.</param>
    /// <param name="outputs" example="[ &quot;dist/**&quot; ]">Default values.</param>
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
    /// Run npm command.
    /// </summary>
    /// <param name="args" example="&quot;--port=1337&quot;">Arguments to pass to target.</param> 
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
    /// Install packages using lock file.
    /// </summary>
    /// <param name="force" example="true">Force install.</param> 
    /// <param name="args" example="&quot;--no-color&quot;">Arguments to pass to target.</param> 
    [<LocalCacheAttribute>]
    [<BatchableAttribute>]
    static member install (context: ActionContext)
                          (force: bool option)
                          (args: string option) =
        let force = force |> map_true "--force"
        let args = args |> or_default ""
        let filters =
            match context.Batch with
            | Some batch -> batch.ProjectPaths |> List.map (fun project -> $"--filter ./{project}") |> String.join " "
            | _ -> ""

        let ops = [
            shellOp("pnpm", $"--recursive {filters} install {force} {args}")
        ]
        ops


    /// <summary>
    /// Run `build` script.
    /// </summary>
    /// <param name="args" example="&quot;--no-color&quot;">Arguments to pass to target.</param> 
    [<RemoteCacheAttribute>]
    [<BatchableAttribute>]
    static member build (context: ActionContext)
                        (args: string option) =
        let args = args |> or_default ""
        let filters =
            match context.Batch with
            | Some batch -> batch.ProjectPaths |> List.map (fun project -> $"--filter ./{project}") |> String.join " "
            | _ -> ""
        let ops = [
            shellOp("pnpm", $"--recursive {filters} run build {args}")   
        ]
        ops


    /// <summary>
    /// Run `test` script.
    /// </summary>
    /// <param name="args" example="&quot;--port=1337&quot;">Arguments to pass to target.</param> 
    [<RemoteCacheAttribute>]
    [<BatchableAttribute>]
    static member test (context: ActionContext)
                       (args: string option) =
        let args = args |> or_default ""
        let filters =
            match context.Batch with
            | Some batch -> batch.ProjectPaths |> List.map (fun project -> $"--filter ./{project}") |> String.join " "
            | _ -> ""
        let ops = [
            shellOp("pnpm", $"--recursive {filters} run test {args}")   
        ]
        ops

    /// <summary>
    /// Run `run` script.
    /// </summary>
    /// <param name="target" example="&quot;build-prod&quot;">Target to invoke.</param> 
    /// <param name="args" example="&quot;build-prod&quot;">Arguments to pass to target.</param>
    /// <param name="no_recursive" example="true">No recursive</param>
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
