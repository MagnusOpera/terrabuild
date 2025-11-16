namespace Terrabuild.Extensions

open Terrabuild.Extensibility
open Converters


/// <summary>
/// Provides build/test helpers for Rust projects using **Cargo** and a `Cargo.toml` at the project root.
/// Infers dependencies from `Cargo.toml` to link Terrabuild graphs and defaults outputs to `target/{debug,release}`.
/// </summary>
type Cargo() =

    /// <summary>
    /// Infers project metadata from `Cargo.toml` (dependencies and default outputs).
    /// </summary>
    /// <param name="ignores" example="[ ]">Default ignore patterns (none by default).</param>
    /// <param name="outputs" example="[ &quot;target/debug/&quot; &quot;target/release/&quot; ]">Default output directories produced by Cargo.</param>
    /// <param name="dependencies" example="[ &lt;&quot;path from project&gt; ]">Local path dependencies extracted from `Cargo.toml`.</param>
    static member __defaults__ (context: ExtensionContext) =
        let projectFile = CargoHelpers.findProjectFile context.Directory
        let dependencies = projectFile |> CargoHelpers.findDependencies 
        let projectInfo =
            { ProjectInfo.Default
              with Outputs = Set [ "target/debug/"; "target/release/" ]
                   Dependencies = dependencies }
        projectInfo


    /// <summary>
    /// Runs an arbitrary Cargo subcommand (Terrabuild action name is forwarded to `cargo`).
    /// </summary>
    /// <param name="args" example="&quot;check --locked&quot;">Additional arguments appended after the subcommand.</param>
    [<NoCacheAttribute>]
    static member __dispatch__ (context: ActionContext)
                               (args: string option) =
        let args = args |> or_default ""
        let ops = [
            shellOp("cargo", $"{context.Command} {args}")
        ]
        ops


    /// <summary title="Build project.">
    /// Builds the project with `cargo build`.
    /// </summary>
    /// <param name="profile" example="&quot;release&quot;">Cargo profile (defaults to `dev`).</param>
    /// <param name="args" example="&quot;--keep-going&quot;">Additional arguments passed to `cargo build`.</param>
    [<RemoteCacheAttribute>]
    static member build (profile: string option)
                        (args: string option) =
        let profile = profile |> map_value (fun profile -> $"--profile {profile}")
        let args = args |> or_default ""

        let ops = [
            shellOp("cargo", $"build {profile} {args}")
        ]
        ops


    /// <summary>
    /// Runs tests with `cargo test`.
    /// </summary>
    /// <param name="profile" example="&quot;release&quot;">Cargo profile for tests (defaults to `dev`).</param>
    /// <param name="args" example="&quot;--blame-hang&quot;">Additional arguments passed to `cargo test`.</param>
    [<RemoteCacheAttribute>]
    static member test (profile: string option)
                       (args: string option) =
        let profile = profile |> map_value (fun profile -> $"--profile {profile}")
        let args = args |> or_default ""

        let ops = [
            shellOp("cargo", $"test {profile} {args}")
        ]
        ops
