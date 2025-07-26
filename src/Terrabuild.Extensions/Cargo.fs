namespace Terrabuild.Extensions

open Terrabuild.Extensibility
open System.IO

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
    /// <param name="arguments" example="&quot;check&quot;">Arguments for command.</param>
    static member __dispatch__ (context: ActionContext) (arguments: string option) =
        let arguments = arguments |> Option.defaultValue ""
        let arguments = $"{context.Command} {arguments}"

        let ops = [ shellOp("cargo", arguments) ]
        execRequest(Cacheability.Always, ops)


    /// <summary title="Build project.">
    /// Build project.
    /// </summary>
    /// <param name="profile" example="&quot;release&quot;">Profile to use to build project. Default is `dev`.</param>
    /// <param name="arguments" example="&quot;--keep-going&quot;">Arguments for command.</param>
    static member build (context: ActionContext) (profile: string option) (arguments: string option) =
        let profile = profile |> Option.defaultValue "dev"
        let arguments = arguments |> Option.defaultValue ""

        let ops = [ shellOp("cargo", $"build --profile {profile} {arguments}") ]
        execRequest(Cacheability.Always, ops)


    /// <summary>
    /// Test project.
    /// </summary>
    /// <param name="profile" example="&quot;release&quot;">Profile for test command.</param>
    /// <param name="arguments" example="&quot;--blame-hang&quot;">Arguments for command.</param>
    static member test (context: ActionContext) (profile: string option) (arguments: string option) =
        let profile = profile |> Option.defaultValue "dev"
        let arguments = arguments |> Option.defaultValue ""

        let ops = [ shellOp("cargo", $"test --profile {profile} {arguments}") ]
        execRequest(Cacheability.Always, ops)
