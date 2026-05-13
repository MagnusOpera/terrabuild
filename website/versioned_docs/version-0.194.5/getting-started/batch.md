---
title: Batch

prev: /docs/getting-started/tasks

---

Despite being smart at task scheduling and caching, Terrabuild can't always beat compiler optimizations when doing batch builds.

For example, .NET is able to build a whole solution and optimize how to restore and build dependencies. However, for that, you need to build and maintain a solution file. Also, you can't easily build a subset of this solution file.

To have the best of both worlds, Terrabuild supports batch builds and delegates the build to dedicated commands. To support this feature, clusters are created from the build graph when:
- All commands used in a target support batch mode
- Commands are exactly the same across targets
- Targets are part of the same dependency chain

Once clusters are identified, Terrabuild asks extensions to craft dedicated batch commands. This way you do not need to maintain a solution file and you can benefit from faster builds transparently.

You can configure targets to build when thee cluster they belong to must build, even if the target itself could be restored from cache. This is particularly useful in clusters when it's faster to build as part of a batch operation than to restore individually. To use this feature, ensure the extension is compatible with batch mode and choose a `batch` mode on targets:
  * `~single` - Build all affected nodes using a single batch (default)
  * `~never` - Build affected nodes without batching
  * `~partition` - Create partitions for affected nodes and build each in its own batch
