---
title: Tasks

prev: /docs/getting-started/caching

---

Now that you understand the [build graph](/docs/getting-started/graph) and [caching](/docs/getting-started/caching), let's see how tasks actually execute.

## What is a Task?

A **task** is a concrete execution unit: "build the `build` target for project X". When you run `terrabuild run build`, Terrabuild creates tasks for each project that defines a `build` target. These tasks become nodes in the build graph.

For each task, Terrabuild must decide: **Build** (execute commands), **Restore** (recover from cache), or **Summary** (report a previous failed cached run).

## How Tasks Use Caching

Each task has a cache key computed from:
- Hash of files in the project
- Project dependencies and their versions
- Variables used for the task

:::info
Note that unless you are using specific information from commit (like `terrabuild.head_commit` or `terrabuild.branch_or_tag` variables), this key is unique across branches. This is a really important concept since it enables significant build optimizations - the same build can be reused across different branches if nothing has changed.
:::

Once the key has been computed, Terrabuild uses the target configuration and following decision tree to decide what to do:

```mermaid
flowchart LR

classDef start fill:black
classDef build stroke-width:4px,stroke:black,fill:white
classDef restore stroke-width:4px,stroke:black,fill:white
classDef summary stroke-width:4px,stroke:black,fill:white
classDef decision stroke:black

start((" "))

force(Force ?)
dependency(Dependency built ?)
cacheable(Cacheable ?)
cache(Cache summary ?)
retry(Retry ?)

restore((Restore))
build((Build))
summary((Summary))

start --> force

force -- yes --> build
force -- no --> dependency

dependency -- yes --> build
dependency -- no --> cacheable

cacheable -- no --> build
cacheable -- yes --> cache

cache -- missing --> build
cache -- success --> restore
cache -- failed --> retry

retry -- yes --> build
retry -- no --> summary

class start start
class build build
class restore restore
class summary summary
class force,cacheable,cache,dependency,retry decision
```

| Condition | Description |
|-----------|-------------|
| `Force` | Either `--force` or `build = ~always` is enabled |
| `Dependency built` | A non-lazy dependency must build |
| `Cacheable` | The target has cacheable artifacts |
| `Cache summary` | Existing cache metadata is missing, successful, or failed |
| `Retry` | `--retry` is enabled for a failed cache summary |

The resulting actions are:

| Action | Description |
|--------|-------------|
| `Build` | Execute the target commands |
| `Restore` | Restore a successful cache hit |
| `Summary` | Report a previous failed cached run without executing commands or restoring outputs |

## Task Execution Flow

1. **Decision Made** - Based on the decision tree above, Terrabuild chooses Build, Restore, or Summary
2. **Execution** - If building, commands run in sequence (or in parallel for independent tasks)
3. **Cache Update** - Results are stored in local cache, and optionally uploaded to Insights for sharing

**Important**: If a task is built (not restored), dependent tasks in the graph are automatically marked for build unless the built task is `build = ~lazy`. This ensures the graph stays consistent when a dependency changes, while still allowing lazy setup targets to run only when another required target needs them.

This is how the graph, caching, and task execution work together to give you fast, correct builds.
