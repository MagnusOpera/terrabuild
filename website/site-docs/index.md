---
linkTitle: Documentation
title: Terrabuild documentation
description: Apply desired-state configuration to build and deployment work.
---

Terrabuild brings desired-state configuration to software delivery in a
monorepo. You request an outcome—built, tested, packaged, planned, or
deployed—and Terrabuild resolves the graph required to reach it.

Your existing tools still compile code, run tests, build images, plan
infrastructure, and apply changes. Terrabuild provides the shared selection,
ordering, execution, and reuse policy around them. Results whose declared
inputs still match are already satisfied; the remaining work runs in dependency
order.

## Choose a path

### Evaluate Terrabuild

Read [Why Terrabuild?](getting-started/why-terrabuild) to see where it fits,
what it replaces, and what remains the responsibility of your build tools and
CI system.

### Run an example

The [quick start](getting-started/quick-start) uses a polyglot playground with
shared libraries, applications, container images, and a Terraform deployment.
It demonstrates local reuse and lets you inspect a deployment graph without
changing infrastructure.

### Model delivery

Start with these guides when adopting Terrabuild in an existing repository:

- [Key concepts](getting-started/key-concepts) defines projects, targets, tasks, and graph nodes.
- [Graph](getting-started/graph) explains selection and dependency resolution.
- [Target policies](getting-started/target-policies) covers caching, artifacts, phases, batching, environments, and locks.
- [Deployment](getting-started/deployment) connects application artifacts to infrastructure targets.
- [`explain`](usage/explain) resolves a run without executing it.

### Understand delivery with Insights

[Terrabuild Insights](insights) is the optional hosted delivery record. It
stores execution graphs and metadata, shares encrypted managed artifacts,
follows changes through environments, generates release notes, and combines
Terrabuild with GitHub activity for engineering signals.

Terrabuild keeps its local graph and cache when Insights is not connected.

## Desired-state configuration, applied to delivery

Desired-state configuration normally describes the result a system should
reach rather than a script of every step to take. Terrabuild applies that idea
to the delivery graph:

- requested targets and the environment express the intended outcome;
- project and target dependencies define what must be true first;
- inputs and artifact policies establish which prior results still satisfy the graph;
- scheduling policies control how the remaining work may execute.

This is convergence over declared delivery inputs and known results. Terrabuild
does not poll a live environment and infer that a deployment is current.
Deployments and cleanup are modeled as side effects that execute whenever they
are selected.

## How convergence works

Every `terrabuild run` command creates an immutable task graph. A node may:

- execute because its inputs or dependencies changed;
- restore files from local or managed cache;
- reuse the successful summary of an externally owned artifact;
- always execute because it represents a side effect;
- remain blocked because a prerequisite failed.

Dependencies keep deployment behind the builds, tests, packages, and plans it
requires. Independent nodes run concurrently.

```hcl
target build {
  depends_on = [ target.^build ]
}

target dist {
  depends_on = [ target.build ]
}

target plan {
  environment_sensitive = true
  depends_on = [ target.^dist ]
}

target deploy {
  build = ~always
  artifacts = ~none
  depends_on = [ target.plan ]
}
```

Here, deployment waits for its environment-specific plan and the distributable
artifacts from upstream projects. The plan can be reused only in the same input
context. Deployment itself always runs and does not create a reusable result.

## Configuration

A repository has two layers of Terrabuild configuration:

- `WORKSPACE` defines shared targets, phases, variables, and extension defaults.
- Each `PROJECT` defines one buildable or deployable unit, its dependencies,
  outputs, and commands.

Configuration uses an HCL-inspired language. Included extensions cover .NET,
Node.js package managers, Docker, Terraform, Gradle, Cargo, Go, Playwright,
OpenAPI, shell commands, and other common tools. FScript extensions can add
repository-specific behavior without recompiling Terrabuild.

## Local and connected operation

Terrabuild runs locally and in CI. Its local cache requires no account.

Connecting [Insights](insights) adds shared encrypted artifacts, graph and run
history, environment timelines, release boundaries, and GitHub-enriched Pulse
metrics. Terrabuild converges the requested graph; Insights preserves the
delivery states that were reached. Once connected, reporting the run lifecycle
is part of the command; artifact transfer failures remain cache failures and do
not discard a completed local entry.
