---
title: Key concepts

prev: /docs/getting-started/quick-start

---

This page connects the main Terrabuild concepts without repeating the full reference.
Use it after the quick start when you want the mental model.

## Core model

### Workspace and project

A **workspace** is the monorepo root. It contains one `WORKSPACE` file with shared configuration such as target policies, phases, variables, and extension defaults.

A **project** is a buildable or deployable unit inside that workspace. Its `PROJECT` file contains outputs, dependencies, labels, and target commands.

```
workspace/
├── WORKSPACE          # Global configuration
├── project-a/
│   └── PROJECT        # Project-specific configuration
└── project-b/
    └── PROJECT
```

### Target, task, and node

| Term | Meaning |
|------|---------|
| Target | A named goal such as `build`, `test`, `dist`, or `deploy`. |
| Task | One concrete target execution for one project. |
| Node | That task represented inside the build graph. |

Relationship: `target` defines intent, `task` is the runtime instance, `node` is the graph representation.

### Extension, action, and command

| Term | Meaning |
|------|---------|
| Extension | A capability provider such as `@dotnet`, `@npm`, or `@terraform`. |
| Action | An operation exposed by an extension, such as `build`, `test`, `plan`, or `apply`. |
| Command | One use of an action inside a target, for example `@terraform plan { }`. |

### Build graph

Terrabuild expands selected targets into a DAG:

- nodes are tasks
- edges are dependencies between tasks
- the graph determines order, parallelism, and rebuild propagation

See [Graph](/docs/getting-started/graph) for the detailed walkthrough.

### Three kinds of dependency

Terrabuild models related but distinct concerns:

| Dependency | What it means | When to use it |
|------------|---------------|----------------|
| **Project dependency** | One project's version and change state depend on another project. It also defines the upstream project set used by `target.^...`. | A project consumes source or artifacts from another project. |
| **Target dependency** | One concrete task must complete before another task. | A specific target needs another target, such as `dist` after `build`. |
| **Phase dependency** | Selecting a downstream phased target enlists every target in prerequisite phases and creates a workspace-wide success barrier. | A whole class of work, such as local toolchains, must finish before application work. |

Project dependencies describe the project graph. Target and phase dependencies produce ordering edges in the task graph; phase dependencies are lowered to ordinary immutable graph edges before selection continues.

### Target dependency resolution

Terrabuild uses target dependency syntax to describe execution order:

- `target.^build`: run `build` for upstream dependency projects first
- `target.build`: run `build` for the current project first

Target references only create dependencies when the referenced target exists in the relevant project scope. Circular target dependency chains are invalid and are reported before commands run.

```hcl
target build {
  depends_on = [ target.^build ]
}

target dist {
  depends_on = [ target.build ]
}
```

Workspace target dependencies and project target dependencies are combined for the same target. See [Workspace Target Block](/docs/workspace/target) and [Project Target Block](/docs/project/target) for the reference form.

### Phases

A **phase** is an optional, workspace-wide ordering boundary. Use one when a whole class of work must finish before another class begins. One example is building local toolchain images before running the application targets that consume them.

Phases are declared in `WORKSPACE`, where they can depend on earlier phases. Targets in `WORKSPACE` or `PROJECT` can then be assigned to one phase. Selecting a phased target also selects every target in its prerequisite phases, and all of those prerequisite targets must succeed before the downstream phase starts.

Use an ordinary target dependency when one specific task needs another specific task. Use a phase when the ordering rule applies across a workspace and must enlist all targets in an earlier phase. Both kinds of dependency become edges in the immutable build graph and are checked together for cycles.

Think of a phase dependency as a **success barrier**, not a filter: Terrabuild enlists the entire prerequisite phase rather than trying to infer which of its targets a downstream command happens to consume. This can intentionally perform more work than a direct dependency. See [Phase Block](../workspace/phase#a-phase-is-a-barrier-not-a-filter) for an illustrated example and guidance on splitting coarse phases.

Targets in the same phase can still run concurrently, subject to their ordinary dependencies. Unphased targets keep the usual scheduling behavior. See [Phase Block](../workspace/phase) for declaration, inheritance, and selection details.

### Change detection and cache keys

Terrabuild hashes project files, dependency state, commands, and evaluated inputs to decide whether a task can be restored or must be built. Because the hash is deterministic, the same work can be reused across machines and branches when inputs match.

A failed cached run is not restored as successful output. Terrabuild reports it as a summary unless you pass `--retry`, which makes the task build again.

See [Tasks](/docs/getting-started/tasks) and [Caching](/docs/getting-started/caching).

### Batch builds

A **cluster** is a group of compatible tasks built together in one batch. This lets extensions such as .NET or pnpm reuse native tooling more efficiently.

Batching respects phase boundaries: targets in different phases, and phased and unphased targets, are never mixed in one batch.

See [Batch](/docs/getting-started/batch) for details.

## Keep reading

- [Graph](/docs/getting-started/graph) for how Terrabuild models work
- [Tasks](/docs/getting-started/tasks) for build-vs-restore decisions
- [Glossary](/docs/getting-started/glossary) for short term definitions
- [Syntax](/docs/syntax) for the file format
