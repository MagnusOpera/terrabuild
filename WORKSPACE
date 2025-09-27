
workspace {
    id = "edd11090a41b0291301431d0"
}


locals {
    is_local_build = terrabuild.configuration == "local"
    target_env = terrabuild.environment ?? "dev"

    dotnet = { config: local.is_local_build && local.target_env == "dev" ? "Debug" : "Release"
               evaluate: local.is_local_build && local.target_env == "dev" }

    runtimes = {
        dotnet: terrabuild.ci ? "linux-x64" : "linux-arm64"
    }

    versions = {
        dotnet_sdk: "9.0.304" # https://mcr.microsoft.com/artifact/mar/dotnet/sdk/tags
    }
}

target build {
    depends_on = [ target.install
                   target.^build ]
}

target test {
    depends_on = [ target.build ]
}

target dist {
    depends_on = [ target.build ]
}

target publish {
    depends_on = [ target.dist ]
}

extension @dotnet {
    container = local.is_local_build ? nothing : "mcr.microsoft.com/dotnet/sdk:${local.versions.dotnet_sdk}"
    batch = true
    defaults {
        runtime = local.runtimes.dotnet
        configuration = local.dotnet.config
        evaluate = local.dotnet.evaluate
    }
}
