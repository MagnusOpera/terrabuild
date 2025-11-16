namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Converters

module GradleHelpers =

    [<Literal>]
    let defaultConfiguration = "Debug"


/// <summary>
/// Provides build support for Gradle projects using the Gradle CLI.
/// Defaults outputs to Gradle's `build/classes/` directory and forwards Terrabuild action names to `gradle`.
/// </summary>
type Gradle() =

    /// <summary>
    /// Declares default project outputs for Gradle builds.
    /// </summary>
    /// <param name="outputs" example="[ &quot;build/classes/&quot; ]">Default output folder produced by Gradle.</param>
    static member __defaults__ () =
        let projectInfo = { ProjectInfo.Default
                            with Outputs = Set [ "build/classes/" ] }
        projectInfo

    /// <summary>
    /// Runs an arbitrary Gradle command (action name is forwarded to `gradle`).
    /// </summary>
    /// <param name="args" example="&quot;clean --info&quot;">Arguments appended after the Gradle command.</param>
    [<NoCacheAttribute>]
    static member __dispatch__ (context: ActionContext)
                               (args: string option) =
        let args = args |> or_default ""
        let ops = [
            shellOp("gradle", $"{context.Command} {args}")
        ]
        ops


    /// <summary>
    /// Invokes `gradle assemble` for the chosen configuration.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration to invoke `assemble`. Default is `Debug`.</param>
    /// <param name="args" example="&quot;--scan&quot;">Additional arguments passed to `gradle assemble`.</param>
    [<RemoteCacheAttribute>]
    static member build (configuration: string option)
                        (args: string option) =
        let configuration = configuration |> Option.defaultValue GradleHelpers.defaultConfiguration
        let args = args |> or_default ""

        let ops = [
            shellOp("gradle", $"assemble {configuration} {args}")
        ]
        ops
