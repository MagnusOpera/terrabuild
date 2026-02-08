module Terrabuild.Extensions.Factory

// well-know provided extensions
// do not forget to add reference when adding new implementation
let systemScripts =
    Map [
        "@cargo", typeof<Cargo>
        "@docker", typeof<Docker>
        "@dotnet", typeof<Dotnet>
        "@gradle", typeof<Gradle>
        "@make", typeof<Make>
        "@npm", typeof<Npm>
        "@npx", typeof<Npx>
        "@null", typeof<Null>
        "@shell", typeof<Shell>
        "@openapi", typeof<OpenApi>
        "@playwright", typeof<Playwright>
        "@pnpm", typeof<Pnpm>
        "@sentry", typeof<Sentry>
        "@terraform", typeof<Terraform>
        "@yarn", typeof<Yarn>
    ]

let systemScriptFiles =
    Map [
        "@cargo", "scripts/cargo.fss"
        "@docker", "scripts/docker.fss"
        "@dotnet", "scripts/dotnet.fss"
        "@gradle", "scripts/gradle.fss"
        "@make", "scripts/make.fss"
        "@npm", "scripts/npm.fss"
        "@npx", "scripts/npx.fss"
        "@openapi", "scripts/openapi.fss"
        "@null", "scripts/null.fss"
        "@playwright", "scripts/playwright.fss"
        "@pnpm", "scripts/pnpm.fss"
        "@sentry", "scripts/sentry.fss"
        "@shell", "scripts/shell.fss"
        "@yarn", "scripts/yarn.fss"
    ]
