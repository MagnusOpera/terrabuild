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
                  with Ignores = Set [ "node_modules/**" ]
                       Outputs = Set [ "dist/**" ]
                       Dependencies = dependencies }
            projectInfo
        with
            exn -> forwardExternalError($"Error while processing project {context.Directory}", exn)

    /// <summary>
    /// Run npm command.
    /// </summary>
    /// <param name="args" example="[ &quot;--port=1337&quot; ]">Arguments to pass to target.</param> 
    static member __dispatch__ (context: ActionContext)
                               (args: string list option) =
        let cmd = context.Command
        let args = args |> concat_quote

        let ops = [
            shellOp("npm", $"{cmd} {args}")
        ]
        ops |> execRequest Cacheability.Never


    /// <summary>
    /// Install packages using lock file.
    /// </summary>
    /// <param name="args" example="[ &quot;--install-strategy&quot; &quot;hoisted&quot; ]">Arguments to pass to target.</param> 
    static member install (force: bool option)
                          (args: string list option) =
        let force = force |> map_true "--force"
        let args = args |> concat_quote

        let ops = [
            shellOp("npm", $"ci {force} {args}")
        ]
        ops |> execRequest Cacheability.Always


    /// <summary>
    /// Run `build` script.
    /// </summary>
    /// <param name="args" example="[ &quot;--port=1337&quot; ]">Arguments to pass to target.</param> 
    static member build (args: string list option) =
        let args = args |> concat_quote

        let ops = [
            shellOp("npm", $"run build -- {args}")   
        ]
        ops |> execRequest Cacheability.Always


    /// <summary>
    /// Run `test` script.
    /// </summary>
    /// <param name="args" example="[ &quot;--port=1337&quot; ]">Arguments to pass to target.</param> 
    static member test (args: string list option) =
        let args = args |> concat_quote

        let ops = [
            shellOp("npm", $"run test -- {args}")   
        ]
        ops |> execRequest Cacheability.Always

    /// <summary>
    /// Run `run` script.
    /// </summary>
    /// <param name="srgs" example="[ &quot;build-prod&quot; ]">Arguments to pass to target.</param> 
    static member run (command: string)
                      (args: string list option) =
        let args = args |> concat_quote

        let ops = [
            shellOp("npm", $"run {command} -- {args}")
        ]
        ops |> execRequest Cacheability.Local

