---
title: Batch

prev: /docs/getting-started/tasks

---

Despite being smart at task scheduling and caching, Terrabuild can't always beat compiler optimizations when doing batch builds.

For example, .NET is able to build a whole solution and optimize how to restore and build dependencies. However, for that, you need to build and maintain a solution file. Also, you can't easily build a subset of this solution file.

To have the best of both worlds, Terrabuild supports batch builds and delegates the build to dedicated commands. To support this feature, clusters are created from the build graph when:
- All commands used in a target support batch mode
- Commands resolve to the same batch cluster
- Nodes are required in the current run
- At least one node in the cluster must build
- The candidate has more than one member
- Adding the batch node would not create an external dependency cycle

Once clusters are identified, Terrabuild asks extensions to craft dedicated batch commands. This way you do not need to maintain a solution file and you can benefit from faster builds transparently.

You can configure targets to build when the cluster they belong to must build, even if the target itself could be restored from cache. This is particularly useful in clusters when it is faster to build as part of a batch operation than to restore individually. To use this feature, ensure the extension is compatible with batch mode and choose a `batch` mode on targets:

- `~single` - Build all required compatible nodes in the cluster using a single batch (default)
- `~never` - Build affected nodes without batching
- `~partition` - Split compatible nodes into dependency-connected partitions and build each partition in its own batch

Batching is decided after Terrabuild has assigned build, restore, and summary actions. A batch can include required restored nodes from the same cluster when at least one cluster member is executing; this lets the extension replace several individual operations with one native batch command.
