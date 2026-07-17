---
linkTitle: "Documentation"
title: Introduction
---

Terrabuild is a low-ceremony build orchestrator for monorepos. It keeps your existing tools—such as .NET, pnpm, Docker, or Terraform—in charge of the actual work while Terrabuild provides one holistic view of projects, targets, dependencies, and artifacts.

For every run, Terrabuild creates an immutable task graph and decides for each selected node whether to execute it, restore it from cache, or report a previous failed result. The same graph controls ordering, parallelism, change propagation, phases, and batch optimization.

Configuration uses a compact HCL-inspired language. A root `WORKSPACE` file defines shared policy, while each buildable unit has a `PROJECT` file that describes its dependencies, outputs, and target commands.

:::info
Terrabuild is free, available on [GitHub](https://github.com/magnusopera/terrabuild), and can run locally or in CI. Local caching works without an account. [Insights](https://insights.magnusopera.io) is optional and adds encrypted cache sharing and build metadata across machines.
:::

## Terrabuild as a build tool

Terrabuild builds only the selected targets and their graph dependencies. Deterministic hashing and caching allow equivalent work to be reused across branches and machines.

Terrabuild creates a graph (more specifically a DAG - Directed Acyclic Graph) based on the provided configurations and performs a minimal build. Terrabuild also provides graph optimizations to make builds faster so you can focus on features instead of plumbing. See [Graph](/docs/getting-started/graph) and [Caching](/docs/getting-started/caching) for details.

In order to verify if something needs to be built, Terrabuild creates a hash for each node of the graph (basically, that's a [Merkle tree](https://en.wikipedia.org/wiki/Merkle_tree)) based on the following information:
* project
* target
* commands, variables and containers used for a target
* hash of project files
* hash of project dependencies

Terrabuild checks the cache using the hash as the key:
* if the key exists, build outputs are recovered and no build is required
* otherwise target execution is triggered and dependents are built in turn

Check [Quick Start](/docs/getting-started/quick-start) for a basic workflow. You can also check [Terrabuild Playground](https://github.com/MagnusOpera/terrabuild-playground) if you want to deep dive without further ado.

## Terrabuild as a deployment tool

But it's not only a build tool: it's also a release tool. Terrabuild - through configurations - is able to manage both configurations (debug and release for example) and also target environments (development, integration, production...).

Deploying is a matter of creating the right deployment [targets](/docs/project/target) for chosen environment.

## Features

- **Familiar HCL-inspired syntax** - no YAML or XML.
- **Minimal (re)build** - Terrabuild computes what has changed and builds only what is required. Insights can provide additional cache hits shared across machines.
- **Parallel execution** - Terrabuild runs tasks in parallel while respecting task dependencies.
- **Minimal changes required** - Terrabuild uses standard tools to build your apps. Developers can continue to use the tools/IDE they love without replicating changes to the build system. No duplicated source of truth.
- **Extensible** - Terrabuild supports script-based extensions with FScript, so custom build actions can be implemented with a sandboxed and reviewable protocol. Container images can isolate toolchains and improve reproducibility.
- **Local reproducible builds** - Local or CI builds are the same when isolation is enforced: adopt a workflow and build anywhere.

## Questions or Feedback?

:::info
  Terrabuild is still in active development.
  Have a question or feedback? Feel free to [open an issue](https://github.com/magnusopera/terrabuild/issues)!
:::

## Next

Dive right into the following sections to get started:

- [Getting Started](getting-started): Learn how to build a monorepo using Terrabuild
- [Graph](getting-started/graph): Understand the build graph
- [Caching](getting-started/caching): Learn how caching works
