namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Converters

module GradleHelpers =

    [<Literal>]
    let defaultConfiguration = "Debug"


/// <summary>
/// Add support for Gradle build.
/// </summary>
type Gradle() =

    /// <summary>
    /// Provides default values for project.
    /// </summary>
    /// <param name="outputs" example="[ &quot;build/classes/&quot; ]">Default values.</param>
    static member __defaults__ () =
        let projectInfo = { ProjectInfo.Default
                            with Outputs = Set [ "build/classes/" ] }
        projectInfo

    /// <summary>
    /// Run a gradle `command`.
    /// </summary>
    /// <param name="__dispatch__" example="clean">Example.</param>
    /// <param name="args" example="[ &quot;&quot; ]">Arguments for command.</param>
    static member __dispatch__ (context: ActionContext)
                               (args: string list option) =
        let args = args |> concat_quote

        let ops = [
            shellOp("gradle", $"{context.Command} {args}")
        ]
        ops |>  execRequest Cacheability.Always


    /// <summary>
    /// Invoke build task `assemble` for `configuration`.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration to invoke `assemble`. Default is `Debug`.</param>
    /// <param name="args" example="[ &quot;&quot; ]">Arguments for command.</param>
    static member build (configuration: string option)
                        (args: string list option) =
        let configuration = configuration |> Option.defaultValue GradleHelpers.defaultConfiguration
        let args = args |> concat_quote

        let ops = [
            shellOp("gradlew", $"assemble {configuration} {args}")
        ]
        ops |> execRequest Cacheability.Always
