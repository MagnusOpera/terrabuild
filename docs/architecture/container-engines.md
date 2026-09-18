# Container Engines

Terrabuild supports four execution paths in `Runner.fs`:

- host execution
- Docker-backed container execution
- Podman-backed container execution
- Apple Container-backed execution through the `container` CLI

This document describes the current container runtime arguments used by each engine.

## Selection rules

- Terrabuild defaults to Docker when no engine is selected.
- `workspace.engine` in `WORKSPACE` forces the engine for that workspace and overrides CLI or Graph UI selection.
- CLI and Graph UI engine selection only applies when `workspace.engine` is not set.
- `host` is the explicit engine name for direct host execution.

## Shared container shape

For containerized operations, Terrabuild always starts from:

```text
run --rm --name terrabuild-<node-slug>-<nonce>
```

Then it adds:

- `--cpus=<n>` when `cpus` is configured on the extension
- `--platform=<platform>` when `platform` is configured on the extension
- `-w /terrabuild/<project-directory>`
- `--entrypoint <operation-command>`

Terrabuild mounts three paths for container execution:

- the Terrabuild home directory to `/terrabuild-home`
- the Terrabuild temp directory to `/terrabuild-tmp`
- the current workspace to `/terrabuild`

Terrabuild also injects generic container env vars:

- `HOME=/terrabuild-home`
- `TERRABUILD_HOME=/terrabuild-home`
- `TMPDIR=/terrabuild-tmp`

In addition:

- variables selected through extension `variables` are forwarded from the host
- `$TERRABUILD_HOME` references in forwarded variable values are expanded to `/terrabuild-home`
- extension `env` entries are passed with `-e <name>` and their concrete values are still provided through the spawned process environment map

## Docker arguments

Docker uses bind mounts with `-v`:

```text
-v <homeDir>:/terrabuild-home
-v <tmpDir>:/terrabuild-tmp
-v <workspace>:/terrabuild
```

Docker always adds:

```text
--net=host --pid=host --ipc=host
```

Docker conditionally adds:

- `--user <uid>:<gid>` on Linux hosts
- `-v /var/run/docker.sock:/var/run/docker.sock` only when the container entrypoint command is `docker`

Docker does not add Linux uid/gid mapping on macOS.

## Podman arguments

Podman uses explicit bind mounts with `--mount`:

```text
--mount type=bind,src=<homeDir>,target=/terrabuild-home
--mount type=bind,src=<tmpDir>,target=/terrabuild-tmp
--mount type=bind,src=<workspace>,target=/terrabuild
```

Podman always adds:

```text
--net=host --pid=host --ipc=host
```

Podman conditionally adds Linux-specific runtime flags:

- `--userns=keep-id`
- `--security-opt label=disable`

These flags are used to keep bind-mounted workspace ownership usable and to reduce SELinux relabel friction on common Linux Podman setups.

Podman does not currently receive a conditional container socket mount.

## Host execution

Terrabuild runs the command directly on the host when the effective engine is `host`, or when an operation has no `image`:

- working directory is the project directory
- command is the operation command
- arguments are the operation arguments

Operations without an `image` always use this path, even when the selected engine is Docker, Podman, or Apple Container.

No container-specific arguments are added in this path.

## Apple Container arguments and lifecycle

The `apple` engine invokes `container run` on Apple silicon with macOS 26 or later.
It uses the shared command shape, CPU/platform options, environment forwarding and
three bind mounts described above. Mounts use `-v source:target`. No host namespace,
Linux user mapping, or Docker socket options are added.

Before a run that executes Apple container operations, Terrabuild checks the host
and calls `container list --quiet` to verify the runtime is available. Dry runs and
runs containing only host operations or cached results do not require the service.
The service must be installed and started separately. Container 1.4.1 is the tested
baseline.

Named Apple containers participate in the same cleanup records as Docker and
Podman. Cancellation, failed processes and abandoned records use `container rm -f`;
already-removed containers count as successful cleanup. Only Terrabuild's recorded
containers are removed; the shared service remains running.

Apple uses its own image store. Engine selection does not translate Docker build
commands. Host networking and automatic port publication are not provided.
`make smoke-test-apple` exercises this backend explicitly; the existing smoke suite
and repository self-build retain their Docker configuration.
