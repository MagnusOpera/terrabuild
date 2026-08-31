---
title: Target block

---

The `target` block defines one named operation for a project. A target can build, test, package, plan, deploy, or run another tool-specific sequence. Its commands run in declaration order after its dependencies succeed.

Use [Target policies](../getting-started/target-policies) to choose values from a use case. This page is the complete project-level syntax and inheritance reference.

## Example usage
```
target build {
    phase = phase.application
    depends_on = [ target.^build ]
    outputs = [ "dist/*" ]
    build = ~auto
    artifacts = ~managed
    environment_sensitive = false

    @npm build { arguments = { configuration: var.config } }
}
```

This example keeps one ownership model: the npm command produces declared filesystem output that Terrabuild can store and restore. A target whose actual result is a Docker image should instead use or inherit `artifacts = ~external`, because the registry or Docker engine owns that image and Terrabuild should reuse only its successful execution summary.

## Argument reference

The following arguments are supported:

* `identifier` - (Mandatory) Identifier of the target. This is the name used to reference the target when running `terrabuild run <target>`.
* `phase` - (Optional) Assign this target to a [phase declared in `WORKSPACE`](../workspace/phase), using `phase.<name>`. If omitted, the target inherits the phase from the matching workspace target. Use `phase = nothing` to explicitly remain unphased.
* `depends_on` - (Optional) Additional target references for this project target. Project-level dependencies are combined with workspace target dependencies for the same target. Use `target.^<name>` for upstream dependency projects and `target.<name>` for the same project. References only add targets that exist in the relevant project scope, and circular target dependency chains are reported during graph construction.
* `outputs` - (Optional) Override default outputs for this target. By default, the value is the set of `outputs` from the project configuration and extensions used in the target. Specifies which files/directories should be cached as build artifacts.
* `build` - (Optional) Override default build mode. By default, the target is built if the hash has changed (`~auto`). Possible values:
  * `~auto` - Build when changes are detected (default)
  * `~always` - Always build, ignoring cache
  * `~lazy` - Build on cache miss when selected explicitly, but realize it as a dependency only when an executing dependent requires it; its execution does not force dependents to rebuild
* `batch` - (Optional) Override default batch mode. Extension must support batch mode to enable this feature. Batching is applied only to required, compatible nodes in a cluster that contains at least one node that must build. Possible values:
  * `~single` - Build all required compatible nodes in the cluster using a single batch (default)
  * `~never` - Build affected nodes without batching
  * `~partition` - Split compatible nodes into dependency-connected partitions and build each partition in its own batch
* `artifacts` - (Optional) Override cacheability of the artifacts. By default, the value is the cacheability of the last command. Possible values:
  * `~none` - Do not cache artifacts
  * `~workspace` - Cache artifacts in workspace cache
  * `~managed` - Cache artifacts in managed cache (Insights)
  * `~external` - The action manages its artifacts externally; Terrabuild caches only the execution summary and never restores artifact files
* `lock` - (Optional) Name a machine-global resource that this target must use exclusively. Targets with the same name are serialized across threads and concurrent Terrabuild processes. The lock covers command execution, batch output staging, and restoration of declared workspace or managed outputs. If omitted, the matching workspace target value is inherited; use `lock = nothing` to opt out. Lock ownership is released automatically when the process terminates.
* `environment_sensitive` - (Optional) Set to `true` to opt this target into consuming environment-sensitive predefined variables. If omitted, the matching workspace target value is inherited; otherwise the target is neutral. A neutral consumer fails before operation resolution. For an opted-in target, sensitive value hashes participate in its cache key. `explain`, debug JSON, and the local console report the resulting sensitivity status.
* `commands` - (Optional) List of commands (actions) to run to complete the target. Commands execute in order. Syntax is `@extension action { arguments }`. Each command is an action provided by an extension (e.g., `@dotnet build`, `@npm install`).

:::warning
  Order of commands is important.
:::
