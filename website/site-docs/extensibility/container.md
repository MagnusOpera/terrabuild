---
title: Container

---

An extension can run actions in a container when `image` is specified. Terrabuild uses the workspace engine (`docker` or `podman`) to launch it:

```hcl
extension @terraform {
    image = "hashicorp/terraform:1.8.4"
    platform = "linux/arm64"
    variables = [ "ARM_*"
                  "MYSECRET" ]
}
```

## Argument Reference

The following arguments are supported:
* `image` - (Optional) The container image to use. Default is `nothing`.
* `platform` - (Optional) The platform for the container image. Default is `nothing` (the current host architecture).
* `cpus` - (Optional) Positive CPU limit passed to the container engine.
* `variables` - (Optional) List of host environment variable names to forward. Supports wildcards. Default is `[]`.
* `env` - (Optional) Environment values added to every action for the extension.

All actions for this extension run in the configured image, providing isolation and avoiding toolchain discrepancies.

:::warning
On macOS, [OrbStack](https://orbstack.dev/) is a compatible Docker engine and is often faster than Docker Desktop for local builds.
:::

## Technical implementation

Terrabuild configures container actions as follows:

* The action command becomes the container entrypoint.
* The container is removed after execution.
* The selected platform and CPU limit are applied when provided.
* The workspace, Terrabuild home, and temporary directories are mounted into the container.
* The working directory is the current project directory.
* Network, IPC, and PID namespaces use host mode.
* Declared environment variables are forwarded to the container.
* With Docker, the Docker socket is mounted only when the action command itself is `docker`.
* On Linux, Docker uses the host user and group IDs; Podman uses `keep-id` user namespaces.
