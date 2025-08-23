namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Errors
open Converters

/// <summary>
/// Provides support for `npm`.
/// </summary>
type Npm() =

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
            shellOp("npm", $"{cmd} {args}")
        ]
        ops


    /// <summary>
    /// Install packages using lock file.
    /// </summary>
    /// <param name="force" example="true">Force install.</param> 
    /// <param name="args" example="&quot;--install-strategy hoisted&quot;">Arguments to pass to target.</param> 
    [<LocalCacheAttribute>]
    static member install (force: bool option)
                          (args: string option) =
        let force = force |> map_true "--force"
        let args = args |> or_default ""

        let ops = [
            shellOp("npm", $"ci {force} {args}")
        ]
        ops


    /// <summary>
    /// Run `build` script.
    /// </summary>
    /// <param name="args" example="&quot;--port=1337&quot;">Arguments to pass to target.</param> 
    [<RemoteCacheAttribute>]
    static member build (args: string option) =
        let args = args |> or_default ""
        let ops = [
            shellOp("npm", $"run build -- {args}")   
        ]
        ops


    /// <summary>
    /// Run `test` script.
    /// </summary>
    /// <param name="args" example="&quot;--port=1337&quot;">Arguments to pass to target.</param> 
    [<RemoteCacheAttribute>]
    static member test (args: string option) =
        let args = args |> or_default ""
        let ops = [
            shellOp("npm", $"run test -- {args}")   
        ]
        ops

    /// <summary>
    /// Run `run` script.
    /// </summary>
    /// <param name="target" example="&quot;build-prod&quot;">Target to invoke.</param> 
    /// <param name="args" example="&quot;build-prod&quot;">Arguments to pass to target.</param> 
    [<LocalCacheAttribute>]
    static member run (target: string)
                      (args: string option) =
        let args = args |> or_default ""
        let ops = [
            shellOp("npm", $"run {target} -- {args}")
        ]
        ops

    /// <summary>
    /// Run `exec` script.
    /// </summary>
    /// <param name="package" example="&quot;hello-world-npm&quot;">Package to exec.</param> 
    /// <param name="args" example="&quot;build-prod&quot;">Arguments to pass to target.</param> 
    [<LocalCacheAttribute>]
    static member exec (package: string)
                       (args: string option) =
        let args = args |> or_default ""
        let ops = [
            shellOp("npm", $"exec -- {package} {args}")
        ]
        ops
