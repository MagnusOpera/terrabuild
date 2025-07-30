namespace Terrabuild.Extensions

open Terrabuild.Extensibility
open System.Xml.Linq
open System.IO


#nowarn "0077" // op_Explicit

module DotnetHelpers =
    open Errors

    let private NsNone = XNamespace.None

    let inline (!>) (x : ^a) : ^b = (((^a or ^b) : (static member op_Explicit : ^a -> ^b) x))

    let private ext2projType = Map [ (".csproj",  "FAE04EC0-301F-11D3-BF4B-00C04F79EFBC")
                                     (".fsproj",  "F2A71F9B-5D33-465A-A702-920D77279786")
                                     (".vbproj",  "F184B08F-C81C-45F6-A57F-5ABD9991F28F") 
                                     (".pssproj", "F5034706-568F-408A-B7B3-4D38C6DB8A32")
                                     (".sqlproj", "00D1A9C2-B5F0-4AF3-8072-F6C62B433612")
                                     (".dcproj",  "E53339B2-1760-4266-BCC7-CA923CBCF16C")]



    let findProjectFile (directory: string) =
        let projects =
            ext2projType.Keys
            |> Seq.map (fun k -> $"*{k}")
            |> Seq.collect (fun ext -> System.IO.Directory.EnumerateFiles(directory, ext))
            |> List.ofSeq
        match projects with
        | [ project ] -> project
        | [] -> raiseInvalidArg "No project found"
        | _ -> raiseInvalidArg "Multiple projects found"

    let findDependencies (projectFile: string) =
        let xdoc = XDocument.Load (projectFile)
        let refs =
            xdoc.Descendants() 
            |> Seq.filter (fun x -> x.Name.LocalName = "ProjectReference")
            |> Seq.map (fun x -> !> x.Attribute(NsNone + "Include") : string | null)
            |> Seq.choose Option.ofObj
            |> Seq.map (fun x -> x.Replace("\\", "/"))
            |> Seq.map (Option.get << FS.parentDirectory)
            |> Seq.distinct
            |> List.ofSeq
        Set refs 

    [<Literal>]
    let defaultConfiguration = "Debug"


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
    /// <param name="arguments" example="&quot;-v&quot;">Arguments for command.</param>
    static member __dispatch__ (context: ActionContext) (arguments: string option) =
        let arguments = arguments |> Option.defaultValue ""
        let arguments = $"{context.Command} {arguments}"

        let ops = [ shellOp("dotnet", arguments) ]
        execRequest(Cacheability.Always, ops)


    /// <summary title="Build project.">
    /// Build project and ensure packages are available first.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration to use to build project. Default is `Debug`.</param>
    /// <param name="parallel" example="1">Max worker processes to build the project.</param>
    /// <param name="log" example="true">Enable binlog for the build.</param>
    /// <param name="arguments" example="&quot;--no-incremental&quot;">Arguments for command.</param>
    static member build (context: ActionContext) (configuration: string option) (``parallel``: int option) (log: bool option) (version: string option) (arguments: string option) =
        let configuration =
            configuration
            |> Option.defaultValue DotnetHelpers.defaultConfiguration

        let logger =
            match log with
            | Some true -> " -bl"
            | _ -> ""

        let maxcpucount =
            match ``parallel`` with
            | Some maxcpucount -> $" -maxcpucount:{maxcpucount}"
            | _ -> ""

        let version =
            match version with
            | Some version -> $" -p:Version={version}"
            | _ -> ""

        let arguments = arguments |> Option.defaultValue ""

        let ops = [
            shellOp("dotnet", $"build --no-dependencies --configuration {configuration} {logger} {maxcpucount} {version} {arguments}")
        ]

        execRequest(Cacheability.Always, ops)


    /// <summary>
    /// Pack a project.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration for pack command.</param>
    /// <param name="version" example="&quot;1.0.0&quot;">Version for pack command.</param>
    /// <param name="arguments" example="&quot;--include-symbols&quot;">Arguments for command.</param>
    static member pack (context: ActionContext) (configuration: string option) (version: string option) (arguments: string option)=
        let configuration = configuration |> Option.defaultValue DotnetHelpers.defaultConfiguration
        let version = version |> Option.defaultValue "0.0.0"
        let arguments = arguments |> Option.defaultValue ""

        let ops = [
            shellOp("dotnet", $"pack --no-build --configuration {configuration} /p:Version={version} /p:TargetsForTfmSpecificContentInPackage= {arguments}")
        ]

        execRequest(Cacheability.Always, ops)

    /// <summary>
    /// Publish a project.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration for publish command.</param>
    /// <param name="runtime" example="&quot;linux-x64&quot;">Runtime for publish.</param>
    /// <param name="trim" example="true">Instruct to trim published project.</param>
    /// <param name="single" example="true">Instruct to publish project as self-contained.</param>
    /// <param name="arguments" example="&quot;--version-suffix beta&quot;">Arguments for command.</param>
    static member publish (context: ActionContext) (configuration: string option) (runtime: string option) (trim: bool option) (single: bool option) (arguments: string option) =
        let configuration = configuration |> Option.defaultValue DotnetHelpers.defaultConfiguration

        let runtime =
            match runtime with
            | Some identifier -> $" -r {identifier}"
            | _ -> " --no-restore --no-build"
        let trim =
            match trim with
            | Some true -> " -p:PublishTrimmed=true"
            | _ -> ""
        let single =
            match single with
            | Some true -> " --self-contained"
            | _ -> ""
        let arguments = arguments |> Option.defaultValue ""

        let ops = [
            shellOp("dotnet", $"publish --no-dependencies --configuration {configuration} {runtime} {trim} {single} {arguments}")
        ]

        execRequest(Cacheability.Always, ops)

    /// <summary>
    /// Restore packages.
    /// </summary>
    /// <param name="projectfile" example="&quot;project.fsproj&quot;">Force usage of project file for publish.</param>
    /// <param name="arguments" example="&quot;--no-dependencies&quot;">Arguments for command.</param>
    static member restore (arguments: string option) =
        let arguments = arguments |> Option.defaultValue ""

        let ops = [ shellOp( "dotnet", $"restore --no-dependencies {arguments}") ]
        execRequest(Cacheability.Local, ops)


    /// <summary>
    /// Test project.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration for publish command.</param>
    /// <param name="filter" example="&quot;TestCategory!=integration&quot;">Run selected unit tests.</param>
    /// <param name="arguments" example="&quot;--blame-hang&quot;">Arguments for command.</param>
    static member test (context: ActionContext) (configuration: string option) (filter: string option) (arguments: string option) =
        let configuration = configuration |> Option.defaultValue DotnetHelpers.defaultConfiguration
        let filter = filter |> Option.map (fun filter -> $" --filter \"{filter}\"") |> Option.defaultValue ""
        let arguments = arguments |> Option.defaultValue ""

        let ops = [
            shellOp("dotnet", $"test --no-build --configuration {configuration} {filter} {arguments}")
        ]

        execRequest(Cacheability.Always, ops)
