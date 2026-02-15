module ScriptRegistry
open Terrabuild.Configuration.AST
open Terrabuild.Expressions

let BuiltInScriptFiles =
    Map [
        "@cargo", "Scripts/cargo.fss"
        "@docker", "Scripts/docker.fss"
        "@dotnet", "Scripts/dotnet.fss"
        "@gradle", "Scripts/gradle.fss"
        "@make", "Scripts/make.fss"
        "@npm", "Scripts/npm.fss"
        "@npx", "Scripts/npx.fss"
        "@null", "Scripts/null.fss"
        "@openapi", "Scripts/openapi.fss"
        "@playwright", "Scripts/playwright.fss"
        "@pnpm", "Scripts/pnpm.fss"
        "@sentry", "Scripts/sentry.fss"
        "@shell", "Scripts/shell.fss"
        "@terraform", "Scripts/terraform.fss"
        "@yarn", "Scripts/yarn.fss"
    ]

let private internalScriptFiles =
    [ "Scripts/_helpers.fss"
      "Scripts/_protocol.fss" ]

let EmbeddedScriptFiles =
    let builtIns =
        BuiltInScriptFiles
        |> Map.toList
        |> List.map snd

    builtIns @ internalScriptFiles
    |> Set.ofList
    |> Set.toList

let private builtInDefaults name =
    match name with
    | "@shell"
    | "@npx" ->
        Some (Map [ "args", Expr.String "" ])
    | _ ->
        None

let SystemExtensions =
    BuiltInScriptFiles
    |> Map.toSeq
    |> Seq.map (fun (name, _) ->
        name,
        { ExtensionBlock.Image = None
          Platform = None
          Variables = None
          Script = None
          Cpus = None
          Defaults = builtInDefaults name
          Env = None })
    |> Map.ofSeq
