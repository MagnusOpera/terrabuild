---
title: Batch

prev: /docs/getting-started/insights

---

Running one command per project is not always the cheapest option. Some toolchains can compile or restore several projects more efficiently with one native command.

For example, .NET can build several projects through one generated solution. Terrabuild can create that batch from the selected graph instead of requiring a permanent solution file for every possible subset.

Terrabuild creates a batch cluster only when:

- All commands used in a target support batch mode
- Commands resolve to the same batch cluster
- Nodes are required in the current run
- At least one node in the cluster must build
- The candidate has more than one member
- Adding the batch node would not create an external dependency cycle

The extension turns each cluster into a tool-specific batch command. Check the extension documentation before enabling batching because the supported actions and grouping rules differ by tool.

A batch may include a target that could have restored from cache when another member must execute. This trade can help when one native batch costs less than a mixture of execution and artifact restoration. The member remains a logical cache reuse: Terrabuild records `restore (batch-cache-reuse)`, links it to the physical command through `batchId`, and does not publish a replacement artifact for that member. The batch result still determines whether every member succeeds. Measure both modes for the workspace before relying on that assumption.

Choose a `batch` mode on the target:

- `~single` - Build all required compatible nodes in the cluster using a single batch (default)
- `~never` - Build affected nodes without batching
- `~partition` - Split compatible nodes into dependency-connected partitions and build each partition in its own batch

Batching is decided after Terrabuild has assigned build, restore, and summary actions. A batch can include required restored nodes from the same cluster when at least one cluster member is executing; this lets the extension replace several individual operations with one native batch command.

## Choose a mode by failure and performance boundaries

| Mode | Choose it when | Tradeoff |
|------|----------------|----------|
| `~single` | The tool handles all required compatible projects efficiently in one invocation and they should share one success or failure boundary. | Usually minimizes tool startup and repeated dependency discovery, but creates the largest batch and failure scope. |
| `~partition` | Independent dependency components should remain isolated while connected projects still benefit from native batching. | Creates more invocations than `~single`, but a disconnected component cannot enlarge or fail another component's batch. |
| `~never` | Every project must have its own command for correctness, project-specific diagnostics, or comparison while investigating batch behavior. | Removes native batching benefits. Individual nodes may still execute concurrently when the graph allows it. |

`batch = ~never` does **not** serialize targets. If separate commands collide on a shared NuGet tool directory, SDK installation, generator cache, or other machine-global resource, use a named [`lock`](./target-policies#serialize-shared-machine-state-with-a-named-lock). A lock controls concurrency; batch mode controls command grouping.

## Examples

### One native build for the selected cluster

```hcl {filename="WORKSPACE"}
target build {
  depends_on = [ target.^build ]
  batch = ~single
}
```

This suits a .NET workspace where one generated solution is generally cheaper than invoking MSBuild once per project. The selected graph still determines which projects enter the generated solution.

### Keep disconnected applications separate

```hcl {filename="WORKSPACE"}
target build {
  depends_on = [ target.^build ]
  batch = ~partition
}
```

Suppose application A depends on library A, while application B depends on library B and there is no dependency path between the pairs. Terrabuild creates one connected batch for each pair instead of one workspace-wide batch.

This is a useful middle ground for large monorepos: native tools retain dependency-aware batching, while unrelated components keep separate timing, failure, and lock boundaries.

### Diagnose or isolate individual commands

```hcl {filename="WORKSPACE"}
target build {
  depends_on = [ target.^build ]
  batch = ~never
}
```

Use this temporarily when comparing performance, locating a tool-specific batch failure, or when an extension command is safe only per project. Do not assume it makes execution sequential.

## Why no batch was created

Run `terrabuild explain <target>` and inspect the selected nodes, actions, batch compatibility, and scheduling outcome. Common reasons are:

- only one compatible required node exists
- every compatible node is already reusable and none needs to execute
- one command does not support batching
- command inputs produce different cluster identities
- target policies use `batch = ~never`
- phase boundaries separate otherwise compatible nodes
- contracting the candidate into a batch would introduce an external dependency cycle

Measure a representative warm and changed build before choosing a workspace policy. A native batch can avoid repeated tool startup, but it can also execute members that could otherwise have restored independently.
