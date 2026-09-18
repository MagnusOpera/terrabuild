---
title: Container

---

An extension can run actions in a container when `image` is specified. Terrabuild uses the workspace engine (`docker`, `podman`, or `apple`) to launch it:

```terrabuild
extension @terraform {
    image = "hashicorp/terraform:1.8.4"
    platform = "linux/arm64"
    variables = [ "ARM_*"
                  "MYSECRET" ]
}
```

## Argument reference

The following arguments are supported:
* `image` - (Optional) The container image to use. Default is `nothing`.
* `platform` - (Optional) The platform for the container image. Default is `nothing` (the current host architecture).
* `cpus` - (Optional) Positive CPU limit passed to the container engine.
* `variables` - (Optional) List of host environment variable names to forward. Supports wildcards. Default is `[]`. The matched names and values affect the target cache identity through a one-way aggregate fingerprint; plaintext values are not stored in cache entries or diagnostics.
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
* Docker and Podman use host network, IPC, and PID namespaces. Apple Container uses its own VM and default network.
* Declared environment variables are forwarded to the container.
* With Docker, the Docker socket is mounted only when the action command itself is `docker`.
* On Linux, Docker uses the host user and group IDs; Podman uses `keep-id` user namespaces.

## Apple Container on macOS

On Apple silicon with macOS 26 or later, install and start Apple's Container tool:

```bash
brew install container
container system start
terrabuild run build --engine apple
```

Accept the recommended Linux kernel installation when prompted. The Apple engine
has been tested with Container 1.4.1. Terrabuild requires the service to be running;
it does not install, start, stop, or upgrade the service automatically.

You can also select `engine = ~apple` in the `workspace` block. A workspace engine
setting overrides CLI and Graph UI selection. Docker remains the default when no
engine is selected. Actions without an `image` still run on the host.

Apple Container uses its own image store and registry credentials. An image built
locally by Docker is not automatically available to Apple Container. Build or
import it with `container`, or pull it from a registry. Selecting `apple` does not
rewrite `@docker` actions or shell commands that invoke Docker.

Apple containers do not receive Docker's host network, PID, IPC, or socket options.
Host `localhost` and automatic development-server port exposure are therefore not
provided. Terrabuild currently uses Apple's default networking without publishing
ports. Prefer native `linux/arm64` images; other platforms depend on Apple's runtime
and any required emulation setup.

To validate the engine on a configured Mac, run `make smoke-test-apple`. This checks
parallel builds, bind-mounted files, environment forwarding, exit codes and Ctrl+C
cleanup using temporary workspaces.
