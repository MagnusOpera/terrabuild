
workspace {
    id = "edd11090a41b0291301431d0"
    engine = ~docker
    configuration = "local"
    environment = "dev"
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
        dotnet_sdk: "10.0.100" # https://mcr.microsoft.com/artifact/mar/dotnet/sdk/tags
        pnpm: "22-10" # https://hub.docker.com/r/guergeiro/pnpm/tags
    }
}

target install {
    outputs = []
    artifacts = ~workspace
    build = ~lazy
}

target build {
    artifacts = ~managed
    depends_on = [ target.install
                   target.^build ]
}

target test {
    artifacts = ~managed
    depends_on = [ target.build ]
}

target dist {
    artifacts = ~external
    depends_on = [ target.build ]
}

extension @dotnet {
    image = local.is_local_build ? nothing : "mcr.microsoft.com/dotnet/sdk:${local.versions.dotnet_sdk}"
    defaults {
        runtime = local.runtimes.dotnet
        configuration = local.dotnet.config
        evaluate = local.dotnet.evaluate
    }
}

extension @pnpm {
    image = local.is_local_build ? nothing : "docker.io/guergeiro/pnpm:${local.versions.pnpm}"
    defaults {
        frozen = true
    }
    env {
        CI = true
    }
}
