---
title: "build"
---
Builds an image tagged with the Terrabuild hash, and pushes it in CI.
```
@docker build {
    image = "ghcr.io/example/project"
    dockerfile = "Dockerfile"
    platforms = "linux/amd64"
    build_args = { configuration: "Release" }
    args = "--debug"
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | external
| Bach      | no

## Argument Reference
The following arguments are supported:
* `image` - (Required) Image repository.
* `dockerfile` - (Optional) Dockerfile path. Default value is `Dockerfile`.
* `platforms` - (Optional) Target platforms. Default value is `host`.
* `build_args` - (Optional) Build arguments (`--build-arg`).
* `args` - (Optional) Extra `docker build` arguments.
