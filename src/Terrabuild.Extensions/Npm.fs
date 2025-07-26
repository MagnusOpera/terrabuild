namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Errors

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
    /// <param name="arguments" example="&quot;--port=1337&quot;">Arguments to pass to target.</param> 
    static member __dispatch__ (context: ActionContext) (arguments: string option) =
        let cmd = context.Command
        let arguments = arguments |> Option.defaultValue ""

        let ops = [
            shellOp("npm", $"{cmd} {arguments}")
        ]
        execRequest(Cacheability.Always, ops)


    /// <summary>
    /// Install packages using lock file.
    /// </summary>
    static member install (context: ActionContext) (force: bool option)=
        let force = if force = Some true then " --force" else ""
        let ops = [ shellOp("npm", $"ci{force}") ]
        execRequest(Cacheability.Always, ops)


    /// <summary>
    /// Run `build` script.
    /// </summary>
    /// <param name="arguments" example="&quot;--port=1337&quot;">Arguments to pass to target.</param> 
    static member build (context: ActionContext) (arguments: string option) =
        let args = arguments |> Option.defaultValue ""

        let ops = [
            shellOp("npm", "ci")
            shellOp("npm", $"run build -- {args}")   
        ]
        execRequest(Cacheability.Always, ops)


    /// <summary>
    /// Run `test` script.
    /// </summary>
    /// <param name="arguments" example="&quot;--port=1337&quot;">Arguments to pass to target.</param> 
    static member test (context: ActionContext) (arguments: string option) =
        let args = arguments |> Option.defaultValue ""

        let ops = [
            shellOp("npm", "ci")
            shellOp("npm", $"run test -- {args}")   
        ]
        execRequest(Cacheability.Always, ops)

    /// <summary>
    /// Run `run` script.
    /// </summary>
    /// <param name="arguments" example="&quot;build-prod&quot;">Arguments to pass to target.</param> 
    static member run (context: ActionContext) (command: string) (arguments: string option) =
        let args = arguments |> Option.defaultValue ""

        let ops = [
            shellOp("npm", $"run {command} -- {args}")
        ]
        execRequest(Cacheability.Always, ops)
