
workspace {
    id = "edd11090a41b0291301431d0"
}


locals {
    is_prod = terrabuild.configuration == "Release"
    configuration = local.is_prod ? "Release" : "Debug"
}

target install {
    cache = "local"
    idempotent = true
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
    container = "mcr.microsoft.com/dotnet/sdk:9.0.304"
    defaults {
        configuration = local.configuration
    }
}
