namespace Terrabuild.Extensions
open Terrabuild.Extensibility

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
    /// <param name="args" example="&quot;--type-mappings ClassA=ClassB&quot;">Additional arguments for generator.</param>
    static member generate (context: ActionContext) (generator: string) (input: string) (output: string) (properties: Map<string, string>) (args: string option) =
        let props =
            if properties |> Map.isEmpty then ""
            else
                let args = properties |> Seq.map (fun kvp -> $"{kvp.Key}={kvp.Value}") |> String.concat ","
                $" --additional-properties={args}"
        let args =
            match args with
            | Some args -> $" {args}"
            | None -> ""

        let args = $"generate -i {input} -g {generator} -o {output}{props}{args}"

        let ops = [
            shellOp("docker-entrypoint.sh", args)
        ]
        execRequest(Cacheability.Always, ops)
