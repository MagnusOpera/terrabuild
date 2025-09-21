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
              with Outputs = Set [ "bin/"; "obj/"; "**/*.binlog" ]
                   Dependencies = dependencies }
        projectInfo


    /// <summary>
    /// Run a dotnet `command`.
    /// </summary>
    /// <param name="__dispatch__" example="run">Example.</param>
    /// <param name="args" example="&quot;-v&quot;">Arguments for command.</param>
    [<NoCacheAttribute>]
    static member __dispatch__ (context: ActionContext)
                               (args: string option) =
        let args = args |> or_default ""
        let ops = [
            shellOp("dotnet", $"{context.Command} {args}")
        ]
        ops

    static member __batch__ () =
        ()

    /// <summary>
    /// Run a dotnet tool.
    /// </summary>
    /// <param name="args" example="&quot;install MagnusOpera.OpenApiGen&quot;">Example.</param>
    [<LocalCacheAttribute>]
    static member tool (args: string option) =
        let args = args |> or_default ""
        let ops = [
            shellOp("dotnet", $"tool {args}")
        ]
        ops


    /// <summary>
    /// Restore packages.
    /// </summary>
    /// <param name="dependencies" example="&quot;true&quot;">Restore dependencies.</param>
    /// <param name="floating" example="&quot;true&quot;">Floating mode restore.</param>
    /// <param name="evaluate" example="&quot;true&quot;">Force package evaluation.</param>
    /// <param name="args" example="&quot;--no-dependencies&quot;">Arguments for command.</param>
    [<LocalCacheAttribute>]
    [<BatchableAttribute>]
    static member restore (dependencies: bool option)
                          (floating: bool option)
                          (evaluate: bool option)
                          (args: string option) =
        let no_dependencies = dependencies |> map_false "--no-dependencies"
        let locked = floating |> map_false "--locked-mode"
        let force_evaluate = evaluate |> map_true "--force-evaluate"
        let args = args |> or_default ""

        let ops = [
            shellOp( "dotnet", $"restore {no_dependencies} {locked} {force_evaluate} {args}")
        ]
        ops


    /// <summary title="Build project.">
    /// Build project.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration to use to build project. Default is `Debug`.</param>
    /// <param name="parallel" example="1">Max worker processes to build the project.</param>
    /// <param name="log" example="true">Enable binlog for the build.</param>
    /// <param name="restore" example="&quot;true&quot;">Restore packages.</param>
    /// <param name="version" example="&quot;1.2.3&quot;">Build version.</param>
    /// <param name="dependencies" example="true">Restore dependencies as well.</param>
    /// <param name="args" example="&quot;--no-incremental&quot;">Arguments for command.</param>
    [<RemoteCacheAttribute>]
    [<BatchableAttribute>]
    static member build (configuration: string option)
                        (``parallel``: int option)
                        (log: bool option)
                        (restore: bool option)
                        (version: string option)
                        (dependencies: bool option)
                        (args: string option) =
        let configuration = configuration |> or_default DotnetHelpers.defaultConfiguration
        let log = log |> map_true "-bl"
        let no_restore = restore |> map_false "--no-restore"
        let maxcpucount = ``parallel`` |> map_value (fun maxcpucount -> $"-maxcpucount:{maxcpucount}")
        let version = version |> map_value (fun version -> $"-p:Version={version}")
        let no_dependencies = dependencies |> map_false "--no-dependencies"
        let args = args |> or_default ""

        let ops = [
            shellOp("dotnet", $"build {no_restore} {no_dependencies} --configuration {configuration} {log} {maxcpucount} {version} {args}")
        ]
        ops


    /// <summary>
    /// Pack project.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration for pack command.</param>
    /// <param name="restore" example="&quot;true&quot;">Restore packages.</param>
    /// <param name="build" example="&quot;true&quot;">Build project.</param>
    /// <param name="version" example="&quot;1.0.0&quot;">Version for pack command.</param>
    /// <param name="args" example="&quot;--include-symbols&quot;">Arguments for command.</param>
    [<RemoteCacheAttribute>]
    static member pack (configuration: string option)
                       (version: string option)
                       (restore: bool option)
                       (build: bool option)
                       (args: string option)=
        let configuration = configuration |> or_default DotnetHelpers.defaultConfiguration
        let version = version |> or_default "0.0.0"
        let no_restore = restore |> map_false "--no-restore"
        let no_build = build |> map_false "--no-build"
        let args = args |> or_default ""

        let ops = [
            shellOp("dotnet", $"pack {no_restore} {no_build} --configuration {configuration} /p:Version={version} /p:TargetsForTfmSpecificContentInPackage= {args}")
        ]
        ops

    /// <summary>
    /// Publish project.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration for publish command.</param>
    /// <param name="restore" example="&quot;true&quot;">Restore packages.</param>
    /// <param name="build" example="&quot;true&quot;">Build project.</param>
    /// <param name="runtime" example="&quot;linux-x64&quot;">Runtime for publish.</param>
    /// <param name="trim" example="true">Instruct to trim published project.</param>
    /// <param name="single" example="true">Instruct to publish project as self-contained.</param>
    /// <param name="args" example="&quot;--version-suffix beta&quot;">Arguments for command.</param>
    [<RemoteCacheAttribute>]
    static member publish (configuration: string option)
                          (restore: bool option)
                          (build: bool option)
                          (runtime: string option)
                          (trim: bool option)
                          (single: bool option)
                          (args: string option) =
        let configuration = configuration |> or_default DotnetHelpers.defaultConfiguration
        let no_restore = restore |> map_false "--no-restore"
        let no_build = build |> map_false "--no-build"
        let runtime = runtime |> map_value (fun runtime -> $"-r {runtime}")
        let trim = trim |> map_true "-p:PublishTrimmed=true"
        let single = single |> map_true "--self-contained"
        let args = args |> or_default ""

        let ops = [
            shellOp("dotnet", $"publish {no_restore} {no_build} --configuration {configuration} {runtime} {trim} {single} {args}")
        ]
        ops

    /// <summary>
    /// Test project.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration for publish command.</param>
    /// <param name="restore" example="&quot;true&quot;">Restore packages.</param>
    /// <param name="build" example="&quot;true&quot;">Build project.</param>
    /// <param name="filter" example="&quot;TestCategory!=integration&quot;">Run selected unit tests.</param>
    /// <param name="args" example="&quot;--blame-hang&quot;">Arguments for command.</param>
    [<RemoteCacheAttribute>]
    static member test (configuration: string option)
                       (restore: bool option)
                       (build: bool option)
                       (filter: string option)
                       (args: string option) =
        let configuration = configuration |> or_default DotnetHelpers.defaultConfiguration
        let no_restore = restore |> map_false "--no-restore"
        let no_build = build |> map_false "--no-build"
        let filter = filter |> map_value (fun filter -> $"--filter \"{filter}\"")
        let args = args |> or_default ""

        let ops = [
            shellOp("dotnet", $"test {no_restore} {no_build} --configuration {configuration} {filter} {args}")
        ]
        ops
