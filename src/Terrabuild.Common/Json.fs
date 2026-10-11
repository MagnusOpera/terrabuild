module Json
open System.Text.Json
open System.Text.Json.Serialization

let private settings =
    let options = JsonSerializerOptions(WriteIndented = true,
                                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                        AllowTrailingCommas = true)
    JsonFSharpOptions.ThothLike()
                        .WithUnwrapOption()
                        .WithUnionTagNamingPolicy(JsonNamingPolicy.CamelCase)
                        .WithSkippableOptionFields(SkippableOptionFields.Always, deserializeNullAsNone = true)
                        .WithAllowNullFields()
                        .AddToJsonSerializerOptions(options)
    options

let Serialize (value: obj) =
    JsonSerializer.Serialize(value, settings)

let Deserialize<'t> (json: string) =
    match JsonSerializer.Deserialize<'t>(json, settings) with
    | null -> Errors.raiseBugError $"Failed to deserialize JSON: {json}"
    | value -> value
