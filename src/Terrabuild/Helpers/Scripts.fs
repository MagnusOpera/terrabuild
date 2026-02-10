module Scripts
open Terrabuild.Configuration.AST
open Terrabuild.Expressions

let BuiltInScriptFiles =
    Map [
        "@cargo", "scripts/cargo.fss"
        "@docker", "scripts/docker.fss"
        "@dotnet", "scripts/dotnet.fss"
        "@gradle", "scripts/gradle.fss"
        "@make", "scripts/make.fss"
        "@npm", "scripts/npm.fss"
        "@npx", "scripts/npx.fss"
        "@null", "scripts/null.fss"
        "@openapi", "scripts/openapi.fss"
        "@playwright", "scripts/playwright.fss"
        "@pnpm", "scripts/pnpm.fss"
        "@sentry", "scripts/sentry.fss"
        "@shell", "scripts/shell.fss"
        "@terraform", "scripts/terraform.fss"
        "@yarn", "scripts/yarn.fss"
    ]

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
