---
title: Container

---

Extension can run actions in a Docker container when `image` is specified on an extension block:

```
extension @terraform {
    image = "hashicorp/terraform:1.8.4"
    platform = "linux/arm64"
    variables = [ "ARM_*"
                  "MYSECRET" ]
}
```
## Argument Reference
The following arguments are supported:
* `image` - (Optional) The Docker image to use. Default is `nothing`.
* `platform` - (Optional) The platform for the Docker image. Default is `nothing` (i.e.: current host architecture).
* `variables` - (Optional) List of variables to pass to Docker instance. Supports wildcards. Default is `[]`.

All actions for this extension will run in the configured image - hence providing both isolation and avoiding toolchains discrepancies.

:::warning
On macOS, it's recommended to use [OrbStack](https://orbstack.dev/) as it's much faster than Docker implementation.
:::

## Technical implementation

All actions are run using following docker configuration:
* Entrypoint and arguments are overriden by action (`--entrypoint`)
* Command runs as PID 1 (`--init`)
* Container is configured for 1Gb of shared memory (`--shm-size=1gb`)
* Container is removed after execution (`--rm`)
* Container uses provided platform - use default if none
* Docker host socket is exposed to container to allow Docker in Docker (`-v /var/run/docker.sock`)
* Container `USER` account is identified and used to map host homedir to USER homedir (`-v`)
* Container `/tmp` is redirected and shared across instances (`v`)
* Container workdir is the current project rootdir (`-w`)
* Network is set to `host` (`--net=host`)
* IPC is set to `host` (`--ipc=host`)
* PID is set to `host` (`--pid=host`)
* Environment variables can be passed from host to container (`-e`) - see variables property of extensions.
