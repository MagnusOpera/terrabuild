---
title: Target Block

---

The `target` block describes how to build a specific target for a project. A target has a unique name in the scope of the PROJECT file (see [identifier](/docs/syntax/identifier)). Targets define the sequence of commands (actions) that need to be executed to produce the desired output, along with dependencies and caching behavior.

## Example Usage
```
target build {
    depends_on = [ target.^build ]
    outputs = [ "dist/*" ]
    build = ~auto
    artifacts = ~workspace

    @npm build { arguments = { configuration: var.config } }
    @docker build { }
}
```

## Argument Reference

The following arguments are supported:

* `identifier` - (Mandatory) Identifier of the target. This is the name used to reference the target when running `terrabuild run <target>`.
* `depends_on` - (Optional) Override the `depends_on` value defined in [WORKSPACE](/docs/workspace/target#argument-reference) for this target. Use `depends_on = [...]` to declare the full dependency set for the project target.
* `outputs` - (Optional) Override default outputs for this target. By default, the value is the set of `outputs` from the project configuration and extensions used in the target. Specifies which files/directories should be cached as build artifacts.
* `build` - (Optional) Override default build mode. By default, the target is built if the hash has changed (`~auto`). Possible values:
  * `~auto` - Build when changes are detected (default)
  * `~always` - Always build, ignoring cache
  * `~lazy` - Build once only when needed by another node
* `batch` - (Option) Override default batch mode. Extension must support batch mode to enable this feature. Possible values:
  * `~single` - Build all affected nodes using a single batch (default)
  * `~never` - Build affected nodes without batching
  * `~partition` - Create partitions for affected nodes and build each in its own batch
* `artifacts` - (Optional) Override cacheability of the artifacts. By default, the value is the cacheability of the last command. Possible values:
  * `~none` - Do not cache artifacts
  * `~workspace` - Cache artifacts in workspace cache
  * `~managed` - Cache artifacts in managed cache (Insights)
  * `~external` - Cache artifacts externally
* `commands` - (Optional) List of commands (actions) to run to complete the target. Commands execute in order. Syntax is `@extension action { arguments }`. Each command is an action provided by an extension (e.g., `@dotnet build`, `@npm install`).

:::warning
  Order of commands is important.
:::
