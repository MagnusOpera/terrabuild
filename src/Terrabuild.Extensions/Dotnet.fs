namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Converters


/// <summary>
/// Provides build, test, pack, and publish helpers for .NET SDK projects (`*.csproj`/`*.fsproj`).
/// Infers project references to wire Terrabuild dependencies and defaults outputs to `bin/`, `obj/`, and build logs.
/// </summary>
type Dotnet() =

    /// <summary>
    /// Infers project metadata from the nearest SDK project file (dependencies and default outputs).
    /// </summary>
    /// <param name="ignores" example="[ &quot;**/*.binlog&quot; ]">Default ignore patterns (binlogs).</param>
    /// <param name="outputs" example="[ &quot;bin/&quot; &quot;obj/&quot; &quot;**/*.binlog&quot; ]">Default outputs produced by dotnet build/publish.</param>
    static member __defaults__ (context: ExtensionContext) =
        let projectFile = DotnetHelpers.findProjectFile context.Directory
        let dependencies = projectFile |> DotnetHelpers.findDependencies 
        let projectInfo =
            { ProjectInfo.Default
              with Outputs = Set [ "bin/"; "obj/"; "**/*.binlog" ]
                   Dependencies = dependencies }
        projectInfo


    /// <summary>
    /// Runs an arbitrary `dotnet` command (action name is forwarded to `dotnet`).
    /// </summary>
    /// <param name="args" example="&quot;run -- -v&quot;">Arguments appended after the dotnet command.</param>
    [<NoCacheAttribute>]
    static member __dispatch__ (context: ActionContext)
                               (args: string option) =
        let args = args |> or_default ""
        let ops = [
            shellOp("dotnet", $"{context.Command} {args}")
        ]
        ops


    /// <summary>
    /// Executes `dotnet tool ...` commands.
    /// </summary>
    /// <param name="args" example="&quot;install MagnusOpera.OpenApiGen&quot;">Arguments appended after `dotnet tool`.</param>
    [<LocalCacheAttribute>]
    static member tool (args: string option) =
        let args = args |> or_default ""
        let ops = [
            shellOp("dotnet", $"tool {args}")
        ]
        ops


    /// <summary>
    /// Restores NuGet packages, optionally frozen, forcing evaluation, or batching workspace projects into a temporary solution.
    /// </summary>
    /// <param name="locked" example="&quot;true&quot;">Enable locked versions; set `true` to add `--locked-mode`.</param>
    /// <param name="evaluate" example="&quot;true&quot;">Add `--force-evaluate` to refresh resolved packages.</param>
    /// <param name="args" example="&quot;--no-dependencies&quot;">Additional arguments for `dotnet restore`.</param>
    [<LocalCacheAttribute>]
    [<BatchableAttribute>]
    static member restore (context: ActionContext)
                          (locked: bool option)
                          (evaluate: bool option)
                          (args: string option) =
        let locked = locked |> map_true "--locked-mode"
        let force_evaluate = evaluate |> map_true "--force-evaluate"
        let args = args |> or_default ""
        let sln =
            match context.Batch with
            | Some batch ->
                let slnFile = FS.combinePath batch.TempDir $"{batch.Hash}.sln"
                DotnetHelpers.writeSolutionFile batch.ProjectPaths DotnetHelpers.defaultConfiguration slnFile
                slnFile
            | _ -> ""

        let ops = [
            shellOp( "dotnet", $"restore {sln} {locked} {force_evaluate} {args}")
        ]
        ops


    /// <summary title="Build project.">
    /// Builds the project or batch solution via `dotnet build`.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration (defaults to `Debug`).</param>
    /// <param name="parallel" example="1">Max worker processes (`-maxcpucount`).</param>
    /// <param name="log" example="true">Emit a binary log (`-bl`).</param>
    /// <param name="restore" example="&quot;true&quot;">Perform restore (omit to add `--no-restore`).</param>
    /// <param name="version" example="&quot;1.2.3&quot;">Set `Version` MSBuild property.</param>
    /// <param name="args" example="&quot;--no-incremental&quot;">Additional arguments for `dotnet build`.</param>
    [<RemoteCacheAttribute>]
    [<BatchableAttribute>]
    static member build (context: ActionContext)
                        (configuration: string option)
                        (``parallel``: int option)
                        (log: bool option)
                        (restore: bool option)
                        (version: string option)
                        (args: string option) =
        let configuration = configuration |> or_default DotnetHelpers.defaultConfiguration
        let log = log |> map_true "-bl"
        let no_restore = restore |> map_false "--no-restore"
        let maxcpucount = ``parallel`` |> map_value (fun maxcpucount -> $"-maxcpucount:{maxcpucount}")
        let version = version |> map_value (fun version -> $"-p:Version={version}")
        let args = args |> or_default ""
        let sln =
            match context.Batch with
            | Some batch ->
                let slnFile = FS.combinePath batch.TempDir $"{batch.Hash}.sln"
                DotnetHelpers.writeSolutionFile batch.ProjectPaths configuration slnFile
                slnFile
            | _ -> ""

        let ops = [
            shellOp("dotnet", $"build {sln} {no_restore} --configuration {configuration} {log} {maxcpucount} {version} {args}")
        ]
        ops


    /// <summary>
    /// Packs the project into a NuGet package via `dotnet pack`.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration (defaults to `Debug`).</param>
    /// <param name="restore" example="&quot;true&quot;">Perform restore (omit to add `--no-restore`).</param>
    /// <param name="build" example="&quot;true&quot;">Build before packing (omit to add `--no-build`).</param>
    /// <param name="version" example="&quot;1.0.0&quot;">Package version (`/p:Version`).</param>
    /// <param name="args" example="&quot;--include-symbols&quot;">Additional arguments for `dotnet pack`.</param>
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
    /// Publishes binaries via `dotnet publish`, optionally self-contained/trimmed and batched via a generated solution.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration (defaults to `Debug`).</param>
    /// <param name="restore" example="&quot;true&quot;">Perform restore (omit to add `--no-restore`).</param>
    /// <param name="build" example="&quot;true&quot;">Build before publish (omit to add `--no-build`).</param>
    /// <param name="runtime" example="&quot;linux-x64&quot;">Runtime identifier (`-r`).</param>
    /// <param name="trim" example="true">Adds `-p:PublishTrimmed=true`.</param>
    /// <param name="single" example="true">Publishes self-contained (`--self-contained`).</param>
    /// <param name="args" example="&quot;--version-suffix beta&quot;">Additional arguments for `dotnet publish`.</param>
    [<RemoteCacheAttribute>]
    [<BatchableAttribute>]
    static member publish (context: ActionContext)
                          (configuration: string option)
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
        let sln =
            match context.Batch with
            | Some batch ->
                let slnFile = FS.combinePath batch.TempDir $"{batch.Hash}.sln"
                DotnetHelpers.writeSolutionFile batch.ProjectPaths configuration slnFile
                slnFile
            | _ -> ""

        let ops = [
            shellOp("dotnet", $"publish {sln} {no_restore} {no_build} --configuration {configuration} {runtime} {trim} {single} {args}")
        ]
        ops

    /// <summary>
    /// Runs tests via `dotnet test`, optionally batched through a generated solution.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration (defaults to `Debug`).</param>
    /// <param name="restore" example="&quot;true&quot;">Perform restore (omit to add `--no-restore`).</param>
    /// <param name="build" example="&quot;true&quot;">Build before testing (omit to add `--no-build`).</param>
    /// <param name="filter" example="&quot;TestCategory!=integration&quot;">Test filter expression (`--filter`).</param>
    /// <param name="args" example="&quot;--blame-hang&quot;">Additional arguments for `dotnet test`.</param>
    [<RemoteCacheAttribute>]
    [<BatchableAttribute>]
    static member test (context: ActionContext)
                       (configuration: string option)
                       (restore: bool option)
                       (build: bool option)
                       (filter: string option)
                       (args: string option) =
        let configuration = configuration |> or_default DotnetHelpers.defaultConfiguration
        let no_restore = restore |> map_false "--no-restore"
        let no_build = build |> map_false "--no-build"
        let filter = filter |> map_value (fun filter -> $"--filter \"{filter}\"")
        let args = args |> or_default ""
        let sln =
            match context.Batch with
            | Some batch ->
                let slnFile = FS.combinePath batch.TempDir $"{batch.Hash}.sln"
                DotnetHelpers.writeSolutionFile batch.ProjectPaths configuration slnFile
                slnFile
            | _ -> ""

        let ops = [
            shellOp("dotnet", $"test {sln} {no_restore} {no_build} --configuration {configuration} {filter} {args}")
        ]
        ops
