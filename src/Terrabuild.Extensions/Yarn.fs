namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Converters


/// <summary>
/// Provides support for `yarn`.
/// </summary>
type Yarn() =

    /// <summary>
    /// Provides default values.
    /// </summary>
    /// <param name="ignores" example="[ &quot;node_modules/**&quot; ]">Default values.</param>
    /// <param name="outputs" example="[ &quot;dist/**&quot; ]">Default values.</param>
    static member __defaults__(context: ExtensionContext) =
        let projectFile = NpmHelpers.findProjectFile context.Directory
        let dependencies = projectFile |> NpmHelpers.findDependencies 
        let projectInfo = 
            { ProjectInfo.Default
              with Outputs = Set [ "dist/**" ]
                   Dependencies = dependencies }
        projectInfo


    /// <summary>
    /// Run yarn `command`.
    /// </summary>
    /// <param name="args" example="&quot;--port=1337&quot;">Arguments to pass to target.</param> 
    [<NoCacheAttribute>]
    static member __dispatch__ (context: ActionContext)
                               (args: string option) =
        let args = args |> or_default ""

        let ops = [
            shellOp("yarn", $"{context.Command} -- {args}")   
        ]
        ops


    /// <summary>
    /// Install packages using lock file.
    /// </summary>
    /// <param name="update" example="true">Restore and update lock file.</param> 
    /// <param name="ignore-engines" example="true">Ignore engines on install.</param> 
    /// <param name="args" example="&quot;--verbose&quot;">Arguments to pass to target.</param> 
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
    /// Run `build` script.
    /// </summary>
    /// <param name="args" example="&quot;--verbose&quot;">Arguments to pass to target.</param> 
    [<RemoteCacheAttribute>]
    static member build (args: string option) =
        let args = args |> or_default ""

        let ops = [
            shellOp("yarn", $"build -- {args}")
        ]
        ops


    /// <summary>
    /// Run `test` script.
    /// </summary>
    /// <param name="args" example="&quot;--verbose&quot;">Arguments to pass to target.</param> 
    [<RemoteCacheAttribute>]
    static member test (args: string option) =
        let args = args |> or_default ""

        let ops = [
            shellOp("yarn", $"test -- {args}")
        ]
        ops

    /// <summary>
    /// Run `run` script.
    /// </summary>
    /// <param name="command" example="&quot;build-prod&quot;">Command to run.</param> 
    /// <param name="args" example="&quot;build-prod&quot;">Arguments to pass to target.</param> 
    [<LocalCacheAttribute>]
    static member run (command: string)
                      (args: string option) =
        let args = args |> or_default ""

        let ops = [
            shellOp("yarn", $"{command} -- {args}")
        ]
        ops
