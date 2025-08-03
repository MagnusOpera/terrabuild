namespace Terrabuild.Extensions

open Terrabuild.Extensibility
open Converters

/// <summary>
/// Add support for Docker projects.
/// </summary>
type Docker() =

    /// <summary>
    /// Run a docker `command`.
    /// </summary>
    /// <param name="__dispatch__" example="image">Example.</param>
    /// <param name="args" example="&quot;prune -f&quot;">Arguments for command.</param>
    static member __dispatch__ (context: ActionContext)
                               (args: string option) =
        let args = args |> or_default ""
        let ops = [
            shellOp("docker", $"{context.Command} {args}")
        ]
        ops |> execRequest Cacheability.Never


    /// <summary>
    /// Build a Dockerfile.
    /// </summary>
    /// <param name="image" required="true" example="&quot;ghcr.io/example/project&quot;">Docker image to build.</param>
    /// <param name="dockerfile" example="&quot;Dockerfile&quot;">Use alternative Dockerfile. Default is Dockerfile.</param>
    /// <param name="platforms" required="false" example="&quot;linux/amd64&quot;">Target platform. Default is host.</param>
    /// <param name="build_args" example="{ configuration: &quot;Release&quot; }">Named arguments to build image (see Dockerfile [ARG](https://docs.docker.com/reference/dockerfile/#arg)).</param> 
    /// <param name="args" example="&quot;--debug&quot;">Arguments for command.</param>
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

        let cacheability =
            if context.CI then Cacheability.Remote
            else Cacheability.Local

        let ops = [
            shellOp("docker", $"build --file {dockerfile} --tag {image}:{context.Hash} {build_args} {platforms} {args} .")
            if context.CI then shellOp("docker", $"push {image}:{context.Hash}")
        ]
        ops |> execRequest cacheability


    /// <summary>
    /// Push target container image to registry.
    /// </summary>
    /// <param name="image" required="true" example="&quot;ghcr.io/example/project&quot;">Docker image to build.</param>
    /// <param name="tag" required="true" example="&quot;1.2.3-stable&quot;">Apply tag on image (use branch or tag otherwise).</param>
    /// <param name="args" example="&quot;--disable-content-trust&quot;">Arguments for command.</param>
    static member push (context: ActionContext)
                       (image: string)
                       (tag: string)
                       (args: string option) =
        let args = args |> or_default ""
        let cacheability =
            if context.CI then Cacheability.Remote
            else Cacheability.Local

        let ops = [
            if context.CI then
                shellOp("docker", $"buildx imagetools create -t {image}:{tag} {image}:{context.Hash} {args}")
            else
                shellOp("docker", $"tag {image}:{context.Hash} {image}:{tag} {args}")
        ]
        ops |> execRequest cacheability
