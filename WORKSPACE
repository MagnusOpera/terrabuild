
workspace {
    id = "edd11090a41b0291301431d0"
    engine = ~docker
    configuration = "local"
    environment = "dev"
}

phase toolchains { }

phase application {
    depends_on = [ phase.toolchains ]
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
        dotnet_sdk: "10.0.302" # https://mcr.microsoft.com/artifact/mar/dotnet/sdk/tags
        node: "22" # https://hub.docker.com/_/node/tags
        pnpm: "10.33.0" # https://pnpm.io/installation
    }
}

target install {
    phase = phase.application
    outputs = []
    artifacts = ~workspace
    build = ~lazy
}

target build {
    phase = phase.application
    artifacts = ~managed
    depends_on = [ target.install
                   target.^build ]
}

target test {
    phase = phase.application
    artifacts = ~managed
    depends_on = [ target.build ]
}

target dist {
    phase = phase.application
    artifacts = ~external
    depends_on = [ target.build ]
}

extension @dotnet {
    image = "ghcr.io/magnusopera/dotnet:${project.dotnet.version}"
    defaults {
        runtime = local.runtimes.dotnet
        configuration = local.dotnet.config
        evaluate = local.dotnet.evaluate
    }
}

extension @docker {
    defaults {
        image = "ghcr.io/magnusopera/${terrabuild.project}"
    }
}

extension @pnpm {
    image = "ghcr.io/magnusopera/pnpm:${project.pnpm.version}"
    defaults {
        frozen = true
    }
    env {
        CI = true
    }
}
