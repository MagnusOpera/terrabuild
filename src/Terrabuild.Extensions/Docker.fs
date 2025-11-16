namespace Terrabuild.Extensions

open Terrabuild.Extensibility
open Converters

/// <summary>
/// Builds and publishes container images using the Docker CLI.
/// Uses the Terrabuild action name as the Docker subcommand for ad-hoc invocations and tags images with the action hash.
/// CI pushes rely on `docker buildx imagetools` to publish the hashed image under an explicit tag.
/// </summary>
type Docker() =

    /// <summary>
    /// Runs an arbitrary Docker CLI command (action name is forwarded to `docker`).
    /// </summary>
    /// <param name="args" example="&quot;image prune -f&quot;">Arguments appended after the Docker subcommand.</param>
    [<NoCacheAttribute>]
    static member __dispatch__ (context: ActionContext)
                               (args: string option) =
        let args = args |> or_default ""
        let ops = [
            shellOp("docker", $"{context.Command} {args}")
        ]
        ops


    /// <summary>
    /// Builds a Docker image and tags it with the Terrabuild hash; pushes automatically when running in CI.
    /// </summary>
    /// <param name="image" required="true" example="&quot;ghcr.io/example/project&quot;">Docker image to build.</param>
    /// <param name="dockerfile" example="&quot;Dockerfile&quot;">Use alternative Dockerfile. Default is Dockerfile.</param>
    /// <param name="platforms" required="false" example="&quot;linux/amd64&quot;">Target platform. Default is host.</param>
    /// <param name="build_args" example="{ configuration: &quot;Release&quot; }">Named arguments to build image (see Dockerfile [ARG](https://docs.docker.com/reference/dockerfile/#arg)).</param> 
    /// <param name="args" example="&quot;--debug&quot;">Additional arguments passed to `docker build`.</param>
    [<ExternalCacheAttribute>]
    static member build (context: ActionContext)
                        (image: string)
                        (dockerfile: string option)
                        (platforms: string list option)
                        (build_args: Map<string, string> option)
                        (args: string option) =
        let dockerfile = dockerfile |> or_default "Dockerfile"
        let platforms = platforms |> format_comma (fun platform -> $"{platform}") |> map_non_empty (fun platforms -> $"--platform {platforms}")
        let build_args = build_args |> format_space (fun kvp -> $"--build-arg {kvp.Key}=\"{kvp.Value}\"")
        let args = args |> or_default ""

        let ops = [
            shellOp("docker", $"build --file {dockerfile} --tag {image}:{context.Hash} {build_args} {platforms} {args} .")
            if context.CI then shellOp("docker", $"push {image}:{context.Hash}")
        ]
        ops


    /// <summary>
    /// Pushes the built image to the registry with a specific tag.
    /// </summary>
    /// <param name="image" required="true" example="&quot;ghcr.io/example/project&quot;">Repository to push.</param>
    /// <param name="tag" required="true" example="&quot;1.2.3-stable&quot;">Tag applied to the image (hash tag is used as the source).</param>
    /// <param name="args" example="&quot;--disable-content-trust&quot;">Additional arguments passed to tagging/push.</param>
    [<ExternalCacheAttribute>]
    static member push (context: ActionContext)
                       (image: string)
                       (tag: string)
                       (args: string option) =
        let args = args |> or_default ""

        let ops = [
            if context.CI then
                shellOp("docker", $"buildx imagetools create -t {image}:{tag} {image}:{context.Hash} {args}")
            else
                shellOp("docker", $"tag {image}:{context.Hash} {image}:{tag} {args}")
        ]
        ops
