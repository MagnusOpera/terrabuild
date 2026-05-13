---
title: Key Concepts

prev: /docs/getting-started/scaffolding

---

This page connects the main Terrabuild concepts without repeating the full reference.
Use it after the quick start when you want the mental model.

## Core Model

### Workspace and Project

A **workspace** is the monorepo root. It contains one `WORKSPACE` file with shared configuration such as target policies, variables, and extension defaults.

A **project** is a buildable unit inside that workspace. It contains one `PROJECT` file with project-specific configuration such as outputs, dependencies, labels, and commands.

```
workspace/
├── WORKSPACE          # Global configuration
├── project-a/
│   └── PROJECT        # Project-specific configuration
└── project-b/
    └── PROJECT
```

### Target, Task, and Node

- **Target**: a named goal such as `build`, `test`, or `dist`.
- **Task**: one concrete target execution for one project.
- **Node**: that task represented inside the build graph.

Relationship: `target` defines intent, `task` is the runtime instance, `node` is the graph representation.

### Extension, Action, and Command

- **Extension**: a capability provider such as `@dotnet`, `@npm`, or `@docker`.
- **Action**: an operation exposed by an extension such as `build`, `test`, or `publish`.
- **Command**: one use of an action inside a target, for example `@dotnet build { }`.

### Build Graph

Terrabuild expands selected targets into a DAG:

- nodes are tasks
- edges are dependencies between tasks
- the graph determines order, parallelism, and rebuild propagation

See [Graph](/docs/getting-started/graph) for the detailed walkthrough.

### Dependency Resolution

Terrabuild uses target dependency syntax to describe execution order:

- `target.^build`: run `build` for upstream dependency projects first
- `target.build`: run `build` for the current project first

```hcl
target build {
  depends_on = [ target.^build ]
}

target dist {
  depends_on = [ target.build ]
}
```

See [Workspace Target Block](/docs/workspace/target) for the reference form.

### Change Detection and Cache Keys

Terrabuild hashes project files, dependency state, commands, and evaluated inputs to decide whether a task can be restored or must be built. Because the hash is deterministic, the same work can be reused across machines and branches when inputs match.

See [Caching](/docs/getting-started/caching) and [Tasks](/docs/getting-started/tasks).

### Batch Builds

A **cluster** is a group of compatible tasks built together in one batch. This lets extensions such as .NET or pnpm reuse native tooling more efficiently.

See [Batch](/docs/getting-started/batch) for details.

## Keep Reading

- [Graph](/docs/getting-started/graph) for how Terrabuild models work
- [Tasks](/docs/getting-started/tasks) for build-vs-restore decisions
- [Glossary](/docs/getting-started/glossary) for short term definitions
- [Syntax](/docs/syntax) for the file format
