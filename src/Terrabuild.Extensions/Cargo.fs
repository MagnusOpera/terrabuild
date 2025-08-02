namespace Terrabuild.Extensions

open Terrabuild.Extensibility
open System.IO
open Converters


module CargoHelpers =
    let findProjectFile dir = FS.combinePath dir "Cargo.toml"

    let findDependencies (projectFile: string) =
        projectFile
        |> File.ReadAllLines
        |> Seq.choose (fun line ->
            match line with
            | String.Regex "path *= *\"(.*)\"" [path] -> Some path
            | _ -> None)
        |> Set.ofSeq


/// <summary>
/// Add support for cargo (rust) projects.
/// </summary>
type Cargo() =

    /// <summary>
    /// Provides default values for project.
    /// </summary>
    /// <param name="ignores" example="[ ]">Default values.</param>
    /// <param name="outputs" example="[ &quot;target/debug/&quot; &quot;target/release/&quot; ]">Default values.</param>
    /// <param name="dependencies" example="[ &lt;&quot;path=&quot; from project&gt; ]">Default values.</param>
    static member __defaults__ (context: ExtensionContext) =
        let projectFile = CargoHelpers.findProjectFile context.Directory
        let dependencies = projectFile |> CargoHelpers.findDependencies 
        let projectInfo =
            { ProjectInfo.Default
              with Ignores = Set [ "target/*" ]
                   Outputs = Set [ "target/debug/"; "target/release/" ]
                   Dependencies = dependencies }
        projectInfo


    /// <summary>
    /// Run a cargo `command`.
    /// </summary>
    /// <param name="__dispatch__" example="format">Example.</param>
    /// <param name="args" example="[ &quot;check&quot; ]">Arguments for command.</param>
    static member __dispatch__ (context: ActionContext)
                               (args: string list option) =
        let args = args |> concat_quote

        let ops = [
            shellOp("cargo", $"{context.Command} {args}")
        ]
        ops |> execRequest Cacheability.Always


    /// <summary title="Build project.">
    /// Build project.
    /// </summary>
    /// <param name="profile" example="&quot;release&quot;">Profile to use to build project. Default is `dev`.</param>
    /// <param name="args" example="[ &quot;--keep-going&quot; ]">Arguments for command.</param>
    static member build (profile: string option)
                        (args: string list option) =
        let profile = profile |> map_default (fun profile -> $"--profile {profile}")
        let args = args |> concat_quote

        let ops = [
            shellOp("cargo", $"build {profile} {args}")
        ]
        ops |> execRequest Cacheability.Always


    /// <summary>
    /// Test project.
    /// </summary>
    /// <param name="profile" example="&quot;release&quot;">Profile for test command.</param>
    /// <param name="args" example="[ &quot;--blame-hang&quot; ]">Arguments for command.</param>
    static member test (profile: string option)
                       (args: string list option) =
        let profile = profile |> map_default (fun profile -> $"--profile {profile}")
        let args = args |> concat_quote

        let ops = [
            shellOp("cargo", $"test {profile} {args}")
        ]
        ops |> execRequest Cacheability.Always
