---
title: Glossary

prev: /docs/getting-started/key-concepts

---

Short definitions for recurring Terrabuild terms.

| Term | Definition | See Also |
|------|------------|----------|
| **Action** | An operation exposed by an extension, such as `build`, `test`, or `publish`. | [Extensions](/docs/extensions) |
| **Artifact** | Files captured after a target runs and stored in cache. | [Project Block](/docs/project/project), [Target Block](/docs/project/target) |
| **Cluster** | A set of compatible tasks grouped into one batch build. | [Batch Builds](/docs/getting-started/batch) |
| **Command** | One action invocation inside a target, for example `@dotnet build { }`. | [Syntax](/docs/syntax) |
| **DAG** | The directed acyclic graph Terrabuild uses to model tasks and dependencies. | [Graph](/docs/getting-started/graph) |
| **Dependency** | A relationship that affects build order or rebuild propagation. | [Workspace Target Block](/docs/workspace/target), [Project Block](/docs/project/project) |
| **Extension** | A built-in or custom capability provider such as `@dotnet` or `@docker`. | [Extensibility](/docs/extensibility), [Workspace Extension Block](/docs/workspace/extension) |
| **Hash** | The deterministic value used as the cache key for a task. | [Caching](/docs/getting-started/caching) |
| **Insights** | The managed backend used for shared Terrabuild cache storage. | [Usage](/docs/usage) |
| **Node** | A task represented in the build graph. | [Graph](/docs/getting-started/graph) |
| **Project** | A buildable unit inside a workspace, defined by a `PROJECT` file. | [Project](/docs/project) |
| **Target** | A named goal such as `build`, `test`, or `dist`. | [Project Target Block](/docs/project/target), [Workspace Target Block](/docs/workspace/target) |
| **Task** | One concrete target execution for one project. | [Tasks](/docs/getting-started/tasks) |
| **Workspace** | The monorepo root, defined by a `WORKSPACE` file. | [Workspace](/docs/workspace) |

## Related Documentation

- [Key Concepts](/docs/getting-started/key-concepts) - Detailed explanations of core concepts
- [Graph](/docs/getting-started/graph) - Build graph structure
- [Caching](/docs/getting-started/caching) - How caching works
- [Syntax](/docs/syntax) - Configuration file syntax reference
- [Quick Start](/docs/getting-started/quick-start) - Hands-on tutorial
