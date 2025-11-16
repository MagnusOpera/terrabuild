namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Converters

/// <summary>
/// Generates API clients using **OpenAPI Generator**.
/// Requires the `openapitools/openapi-generator-cli` container in the extension configuration so the `docker-entrypoint.sh` is available.
/// </summary>
type OpenApi() =

    /// <summary>
    /// Generates an API client via `openapi-generator-cli generate`.
    /// </summary>
    /// <param name="generator" required="true" example="&quot;typescript-axios&quot;">Use provided generator.</param>
    /// <param name="input" required="true" example="&quot;src/api.json&quot;">Relative path to the OpenAPI/Swagger definition.</param>
    /// <param name="output" required="true" example="&quot;src/api/client&quot;">Relative output path for generated sources.</param>
    /// <param name="properties" example="{ withoutPrefixEnums: &quot;true&quot; }">Additional generator properties (comma-joined into `--additional-properties`).</param>
    /// <param name="args" example="&quot;--type-mappings ClassA=ClassB&quot;">Extra arguments forwarded to `openapi-generator`.</param>
    [<RemoteCacheAttribute>]
    static member generate (generator: string)
                           (input: string)
                           (output: string)
                           (properties: Map<string, string> option)
                           (args: string option) =
        let properties = properties |> format_comma (fun kvp -> $"{kvp.Key}={kvp.Value}") |> map_non_empty (fun x -> "--additional-properties={x}")
        let args = args |> or_default ""

        let ops = [
            shellOp("docker-entrypoint.sh", $"generate -i {input} -g {generator} -o {output} {properties} {args}")
        ]
        ops
