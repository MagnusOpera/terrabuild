namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Converters


/// <summary>
/// Add support for .net projects.
/// </summary>
type Dotnet() =

    /// <summary>
    /// Provides default values for project.
    /// </summary>
    /// <param name="ignores" example="[ &quot;**/*.binlog&quot; ]">Default values.</param>
    /// <param name="outputs" example="[ &quot;bin/&quot; &quot;obj/&quot; &quot;**/*.binlog&quot; ]">Default values.</param>
    /// <param name="dependencies" example="[ &lt;ProjectReference /&gt; from project ]">Default values.</param>
    static member __defaults__ (context: ExtensionContext) =
        let projectFile = DotnetHelpers.findProjectFile context.Directory
        let dependencies = projectFile |> DotnetHelpers.findDependencies 
        let projectInfo =
            { ProjectInfo.Default
              with Ignores = Set [ "**/*.binlog"; "TestResults/" ]
                   Outputs = Set [ "bin/"; "obj/"; "**/*.binlog"; "obj/*.json"; "obj/*.props"; "obj/*.targets" ]
                   Dependencies = dependencies }
        projectInfo


    /// <summary>
    /// Run a dotnet `command`.
    /// </summary>
    /// <param name="__dispatch__" example="run">Example.</param>
    /// <param name="args" example="[ &quot;-v&quot; ]">Arguments for command.</param>
    static member __dispatch__ (context: ActionContext)
                               (args: string list option) =
        let args = args |> concat_quote

        let ops = [
            shellOp("dotnet", $"{context.Command} {args}")
        ]
        ops |> execRequest Cacheability.Never


    /// <summary>
    /// Restore packages.
    /// </summary>
    /// <param name="projectfile" example="&quot;project.fsproj&quot;">Force usage of project file for publish.</param>
    /// <param name="dependencies" example="&quot;true&quot;">Restore dependencies.</param>
    /// <param name="locked" example="&quot;true&quot;">Locked mode restore.</param>
    /// <param name="args" example="[ &quot;--no-dependencies&quot; ]">Arguments for command.</param>
    static member restore (dependencies: bool option)
                          (locked: bool option)
                          (args: string list option) =
        let no_dependencies = dependencies |> map_false "--no-dependencies"
        let locked = locked |> map_true "--locked-mode"
        let args = args |> concat_quote

        let ops = [
            shellOp( "dotnet", $"restore {no_dependencies} {locked} {args}")
        ]
        ops |>  execRequest Cacheability.Local


    /// <summary title="Build project.">
    /// Build project.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration to use to build project. Default is `Debug`.</param>
    /// <param name="parallel" example="1">Max worker processes to build the project.</param>
    /// <param name="log" example="true">Enable binlog for the build.</param>
    /// <param name="restore" example="&quot;true&quot;">Restore packages.</param>
    /// <param name="dependencies" example="true">Restore dependencies as well.</param>
    /// <param name="args" example="[ &quot;--no-incremental&quot; ]">Arguments for command.</param>
    static member build (configuration: string option)
                        (``parallel``: int option)
                        (log: bool option)
                        (restore: bool option)
                        (version: string option)
                        (dependencies: bool option)
                        (args: string list option) =
        let configuration = configuration |> or_default DotnetHelpers.defaultConfiguration
        let log = log |> map_true "-bl"
        let no_restore = restore |> map_false "--no-restore"
        let maxcpucount = ``parallel`` |> map_value (fun maxcpucount -> $"-maxcpucount:{maxcpucount}")
        let version = version |> map_value (fun version -> $"-p:Version={version}")
        let no_dependencies = dependencies |> map_false "--no-dependencies"
        let args = args |> concat_quote

        let ops = [
            shellOp("dotnet", $"build {no_restore} {no_dependencies} --configuration {configuration} {log} {maxcpucount} {version} {args}")
        ]
        ops |>  execRequest Cacheability.Always


    /// <summary>
    /// Pack project.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration for pack command.</param>
    /// <param name="restore" example="&quot;true&quot;">Restore packages.</param>
    /// <param name="build" example="&quot;true&quot;">Build project.</param>
    /// <param name="version" example="&quot;1.0.0&quot;">Version for pack command.</param>
    /// <param name="args" example="[ &quot;--include-symbols&quot; ]">Arguments for command.</param>
    static member pack (configuration: string option)
                       (version: string option)
                       (restore: bool option)
                       (build: bool option)
                       (args: string list option)=
        let configuration = configuration |> or_default DotnetHelpers.defaultConfiguration
        let version = version |> or_default "0.0.0"
        let no_restore = restore |> map_false "--no-restore"
        let no_build = build |> map_false "--no-build"
        let args = args |> concat_quote

        let ops = [
            shellOp("dotnet", $"pack {no_restore} {no_build} --configuration {configuration} /p:Version={version} /p:TargetsForTfmSpecificContentInPackage= {args}")
        ]
        ops |> execRequest Cacheability.Always

    /// <summary>
    /// Publish project.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration for publish command.</param>
    /// <param name="restore" example="&quot;true&quot;">Restore packages.</param>
    /// <param name="build" example="&quot;true&quot;">Build project.</param>
    /// <param name="runtime" example="&quot;linux-x64&quot;">Runtime for publish.</param>
    /// <param name="trim" example="true">Instruct to trim published project.</param>
    /// <param name="single" example="true">Instruct to publish project as self-contained.</param>
    /// <param name="args" example="[ &quot;--version-suffix&quot; &quot;beta&quot; ]">Arguments for command.</param>
    static member publish (configuration: string option)
                          (restore: bool option)
                          (build: bool option)
                          (runtime: string option)
                          (trim: bool option)
                          (single: bool option)
                          (args: string list option) =
        let configuration = configuration |> or_default DotnetHelpers.defaultConfiguration
        let no_restore = restore |> map_false "--no-restore"
        let no_build = build |> map_false "--no-build"
        let runtime = runtime |> map_value (fun runtime -> $"-r {runtime}")
        let trim = trim |> map_true "-p:PublishTrimmed=true"
        let single = single |> map_true "--self-contained"
        let args = args |> concat_quote

        let ops = [
            shellOp("dotnet", $"publish {no_restore} {no_build} --configuration {configuration} {runtime} {trim} {single} {args}")
        ]
        ops |>  execRequest Cacheability.Always

    /// <summary>
    /// Test project.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration for publish command.</param>
    /// <param name="restore" example="&quot;true&quot;">Restore packages.</param>
    /// <param name="build" example="&quot;true&quot;">Build project.</param>
    /// <param name="filter" example="&quot;TestCategory!=integration&quot;">Run selected unit tests.</param>
    /// <param name="args" example="[ &quot;--blame-hang&quot; ]">Arguments for command.</param>
    static member test (configuration: string option)
                       (restore: bool option)
                       (build: bool option)
                       (filter: string option)
                       (args: string list option) =
        let configuration = configuration |> or_default DotnetHelpers.defaultConfiguration
        let no_restore = restore |> map_false "--no-restore"
        let no_build = build |> map_false "--no-build"
        let filter = filter |> map_value (fun filter -> $"--filter \"{filter}\"")
        let args = args |> concat_quote

        let ops = [
            shellOp("dotnet", $"test {no_restore} {no_build} --configuration {configuration} {filter} {args}")
        ]
        ops |>  execRequest Cacheability.Always
