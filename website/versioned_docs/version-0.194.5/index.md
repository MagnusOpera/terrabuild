---
linkTitle: "Documentation"
title: Introduction
---

Terrabuild addresses friction faced in existing monorepo build systems:
* **Intrusive tooling** - Many systems require dedicated teams and significant operational overhead
* **Weak dependency graph control** - Limited visibility and control over build dependencies and execution order
* **Sub-optimal caching** - Inefficient cache utilization leading to unnecessary builds

Terrabuild does not replace your build tools. Instead, it orchestrates them to enforce consistent and fast build workflows. Terrabuild operates at the graph level, focusing on files, dependencies, and the build graph structure. It determines what needs to be built based on change detection and cache analysis.

Terrabuild is voluntarily limited and aims at replicating DSC for infrastructure (Desired State Configuration) to build systems. This is why Terrabuild uses an HCL-inspired syntax (disclaimer, it's not pure HCL) to describe how the build should be done and what to expect - threading cache and reuse capabilities throughout the build graph.

:::info
  Terrabuild is free and available at [ GitHub](https://github.com/magnusopera/terrabuild) and can be used locally or in CI.
  
  You will need an account [Insights](https://insights.magnusopera.io) if you want to leverage caching capabilities.
:::

## Terrabuild as a build tool

Terrabuild is a build system designed for monorepos. It aims at being easier, far less intrusive than alternatives and without lock-in.

Terrabuild only builds what's required thanks to heavy caching. It uses a familiar HCL inspired syntax (but not strict HCL) and puts a strong emphasis on using same tools for local development or CI/CD.

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

## Caching and build optimizations

Terrabuild is able to leverage a build cache to avoid building project if nothing has changed. To use this feature, you will need to create an account on [Insights](https://insights.magnusopera.io) and a workspace to hold your artifacts securely.

## Features

- **familiar HCL inspired syntax** - no YAML, no XML.
- **Minimal (re)build** - Terrabuild computes what has changed, and triggers a build for only what has changed. If connected to Insights, cache is leveraged and build will be even faster.
- **Parallel execution** - Terrabuild run tasks in parallel while respecting task dependencies.
- **Minimal changes required** - Terrabuild uses standard tools to build your apps. Developers can continue to use the tools/IDE they love without replicating changes to the build system. No duplicated source of truth.
- **Extensible** - Terrabuild supports script-based extensions with FScript, so custom build actions can be implemented with a sandboxed and reviewable protocol. Terrabuild promotes the usage of Docker images to build apps and isolate your build environment. This also enforces reproducibility of your builds and eliminates configuration discrepancies.
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
