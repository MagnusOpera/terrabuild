namespace Terrabuild.Extensions
open Terrabuild.Extensibility


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
              with Ignores = Set [ "node_modules/**" ]
                   Outputs = Set [ "dist/**" ]
                   Dependencies = dependencies }
        projectInfo


    /// <summary>
    /// Run yarn `command`.
    /// </summary>
    /// <param name="arguments" example="&quot;--port=1337&quot;">Arguments to pass to target.</param> 
    static member __dispatch__ (context: ActionContext) (arguments: string option) =
        let arguments = arguments |> Option.defaultValue ""
        let cmd = context.Command

        let ops = [
            shellOp("yarn", $"{cmd} -- {arguments}")   
        ]
        execRequest(Cacheability.Always, ops)


    /// <summary>
    /// Install packages using lock file.
    /// </summary>
    /// <param name="ignore-engines" example="true">Ignore engines on install.</param> 
    static member install (context: ActionContext) (``ignore-engines``: bool option) =
        let ignoreEngines =
            match ``ignore-engines`` with
            | Some true -> " --ignore-engines"
            | _ -> ""

        let ops = [ shellOp("yarn", $"install --frozen-lockfile{ignoreEngines}") ]
        execRequest(Cacheability.Local, ops)


    /// <summary>
    /// Run `build` script.
    /// </summary>
    /// <param name="arguments" example="&quot;--port=1337&quot;">Arguments to pass to target.</param> 
    /// <param name="ignore-engines" example="true">Ignore engines on install.</param> 
    static member build (context: ActionContext) (arguments: string option) (``ignore-engines``: bool option) =
        let args = arguments |> Option.defaultValue ""
        let ignoreEngines =
            match ``ignore-engines`` with
            | Some true -> " --ignore-engines"
            | _ -> ""

        let ops = [
            shellOp("yarn", $"install --frozen-lockfile{ignoreEngines}")
            shellOp("yarn", $"build -- {args}")
        ]
        execRequest(Cacheability.Always, ops)


    /// <summary>
    /// Run `test` script.
    /// </summary>
    /// <param name="arguments" example="&quot;--port=1337&quot;">Arguments to pass to target.</param> 
    /// <param name="ignore-engines" example="true">Ignore engines on install.</param> 
    static member test (context: ActionContext) (arguments: string option) (``ignore-engines``: bool option) =
        let args = arguments |> Option.defaultValue ""
        let ignoreEngines =
            match ``ignore-engines`` with
            | Some true -> " --ignore-engines"
            | _ -> ""

        let ops = [
            shellOp("yarn", $"install --frozen-lockfile{ignoreEngines}")
            shellOp("yarn", $"test -- {args}")
        ]
        execRequest(Cacheability.Always, ops)

    /// <summary>
    /// Run `run` script.
    /// </summary>
    /// <param name="arguments" example="&quot;build-prod&quot;">Arguments to pass to target.</param> 
    static member run (context: ActionContext) (command: string) (arguments: string option) =
        let args = arguments |> Option.defaultValue ""

        let ops = [
            shellOp("yarn", $"{command} -- {args}")
        ]
        execRequest(Cacheability.Always, ops)
