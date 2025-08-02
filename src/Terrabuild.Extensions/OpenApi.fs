namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Converters

/// <summary>
/// Provides support for `OpenAPI Generator`.
/// 
/// You must use container `openapitools/openapi-generator-cli` in the extension configuration.
/// </summary>
type OpenApi() =

    /// <summary>
    /// Generate api client using `openapi-generator-cli`.
    /// </summary>
    /// <param name="generator" required="true" example="&quot;typescript-axios&quot;">Use provided generator.</param>
    /// <param name="input" required="true" example="&quot;src/api.json&quot;">Relative path to api json file</param>
    /// <param name="output" required="true" example="&quot;src/api/client&quot;">Relative output path.</param>
    /// <param name="properties" example="{ withoutPrefixEnums: &quot;true&quot; }">Additional properties for generator.</param>
    /// <param name="args" example="[ &quot;--type-mappings&quot; &quot;ClassA=ClassB&quot;&quot; ]">Additional arguments for generator.</param>
    static member generate (generator: string)
                           (input: string)
                           (output: string)
                           (properties: Map<string, string> option)
                           (args: string list option) =
        let properties = properties |> format_comma (fun kvp -> $"{kvp.Key}={kvp.Value}") |> map_value (fun x -> "--additional-properties={x}")
        let args = args |> concat_quote

        let ops = [
            shellOp("docker-entrypoint.sh", $"generate -i {input} -g {generator} -o {output} {properties} {args}")
        ]
        execRequest(Cacheability.Always, ops)
