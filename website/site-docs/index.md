---
linkTitle: "Documentation"
title: Introduction
---

Terrabuild coordinates builds and deployments in a monorepo. Your existing tools still compile code, run tests, build images, plan infrastructure, and apply changes. Terrabuild decides which targets must run and in what order.

Every `terrabuild run` command creates an immutable task graph. A node can execute, restore artifacts from cache, or report an earlier failed result. Dependencies keep deployment behind the builds, tests, packages, and infrastructure plans it requires.

Configuration uses an HCL-inspired language. A root `WORKSPACE` file holds shared target policy. Each buildable or deployable unit has a `PROJECT` file with its commands, dependencies, and outputs.

:::info
Terrabuild runs locally and in CI. The local cache requires no account. [Insights](https://insights.magnusopera.io) is optional and adds encrypted cache sharing and build metadata across machines. The source is available on [GitHub](https://github.com/magnusopera/terrabuild).
:::

## Build and deploy with targets

A target is a named operation such as `build`, `test`, `dist`, `plan`, or `deploy`. Targets use the same dependency rules at every stage.

```hcl
target build {
  depends_on = [ target.^build ]
}

target dist {
  depends_on = [ target.build ]
}

target deploy {
  build = ~always
  artifacts = ~none
  depends_on = [ target.plan target.^dist ]
}
```

In this example, deployment waits for its infrastructure plan and for distributable artifacts from upstream projects. If a prerequisite fails, Terrabuild does not run the dependent deployment target.

Read [Deployment](getting-started/deployment) for a complete example with environment selection and Terraform.

## Cache work that can be reused

Terrabuild fingerprints the files, dependency state, commands, and evaluated inputs known to each target. A matching fingerprint can restore saved outputs instead of executing the target again. Targets with side effects, such as deployment, should use `build = ~always` and `artifacts = ~none`.

Read [Caching](/docs/getting-started/caching) for the cache-key inputs and artifact modes.

## Keep the tools already in the repository

Extensions translate target actions into shell operations. Terrabuild includes extensions for .NET, npm, Docker, Terraform, and other common tools. FScript extensions can add commands for tools that are not included.

Start with one of these pages:

- [Quick start](getting-started/quick-start) runs a working monorepo example.
- [Deployment](getting-started/deployment) connects application artifacts to an infrastructure target.
- [Graph](getting-started/graph) explains selection, ordering, and execution.
- [Workspace](workspace) and [Project](project) document the configuration files.
