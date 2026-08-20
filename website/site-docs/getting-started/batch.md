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

A batch may include a target that could have restored from cache when another member must execute. This trade can help when one native batch costs less than a mixture of execution and artifact restoration. Measure both modes for the workspace before relying on that assumption.

Choose a `batch` mode on the target:

- `~single` - Build all required compatible nodes in the cluster using a single batch (default)
- `~never` - Build affected nodes without batching
- `~partition` - Split compatible nodes into dependency-connected partitions and build each partition in its own batch

Batching is decided after Terrabuild has assigned build, restore, and summary actions. A batch can include required restored nodes from the same cluster when at least one cluster member is executing; this lets the extension replace several individual operations with one native batch command.
