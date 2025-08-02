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
    /// <param name="args" example="[ &quot;prune -f&quot; ]">Arguments for command.</param>
    static member __dispatch__ (context: ActionContext)
                               (args: string list option) =
        let args = args |> concat_quote

        let ops = [ shellOp("docker", $"{context.Command} {args}") ]
        execRequest(Cacheability.Always, ops)


    /// <summary>
    /// Build a Dockerfile.
    /// </summary>
    /// <param name="dockerfile" example="&quot;Dockerfile&quot;">Use alternative Dockerfile. Default is Dockerfile.</param>
    /// <param name="image" required="true" example="&quot;ghcr.io/example/project&quot;">Docker image to build.</param>
    /// <param name="platforms" required="false" example="&quot;linux/amd64&quot;">Target platform. Default is host.</param>
    /// <param name="arguments" example="{ configuration: &quot;Release&quot; }">Named arguments to build image (see Dockerfile [ARG](https://docs.docker.com/reference/dockerfile/#arg)).</param> 
    /// <param name="args" example="[ &quot;--debug&quot; ]">Arguments for command.</param>
    static member build (context: ActionContext)
                        (dockerfile: string option)
                        (image: string)
                        (platforms: string list option)
                        (arguments: Map<string, string> option)
                        (args: string list option) =
        let dockerfile = dockerfile |> or_default "Dockerfile"

        let platforms = platforms |> format_space (fun platform -> $"--platform {platform}")
        let arguments = arguments |> format_space (fun kvp -> $"--build-arg {kvp.Key}=\"{kvp.Value}\"")
        let args = args |> concat_quote

        let ops = 
            [
                let buildArgs = $"build --file {dockerfile} --tag {image}:{context.Hash} {arguments} {platforms} {args} ."
                shellOp("docker", buildArgs)
                if context.CI then shellOp("docker", $"push {image}:{context.Hash}")
            ]

        let cacheability =
            if context.CI then Cacheability.Remote
            else Cacheability.Local

        execRequest(cacheability, ops)


    /// <summary>
    /// Push target container image to registry.
    /// </summary>
    /// <param name="image" required="true" example="&quot;ghcr.io/example/project&quot;">Docker image to build.</param>
    /// <param name="tag" required="true" example="&quot;1.2.3-stable&quot;">Apply tag on image (use branch or tag otherwise).</param>
    /// <param name="args" example="[ &quot;--disable-content-trust&quot; ]">Arguments for command.</param>
    static member push (context: ActionContext)
                       (image: string)
                       (tag: string)
                       (args: string list option) =
        let args = args |> concat_quote

        let ops =
            [
                if context.CI then
                    shellOp("docker", $"buildx imagetools create -t {image}:{tag} {image}:{context.Hash} {args}")
                else
                    shellOp("docker", $"tag {image}:{context.Hash} {image}:{tag} {args}")
            ]

        let cacheability =
            if context.CI then Cacheability.Remote
            else Cacheability.Local

        execRequest(cacheability, ops)
