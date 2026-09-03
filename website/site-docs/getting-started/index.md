---
title: Start here
description: Evaluate Terrabuild, install it, and run a complete delivery example.
---

Terrabuild models software delivery as a dependency graph. Before writing
configuration, decide whether that model addresses the difficult part of your
repository.

1. Read [Why Terrabuild?](./why-terrabuild) for the product boundary and common use cases.
2. [Install Terrabuild](./install) on a developer machine or CI runner.
3. Follow the [quick start](./quick-start) to build a polyglot workspace and inspect a deployment.

The playground includes .NET and web applications, shared libraries, container
images, and a Terraform project. It is designed to show the complete graph, not
only compilation.

After the quick start:

- [Deployment](./deployment) explains environments, plans, artifacts, and side effects.
- [Target policies](./target-policies) helps choose cache and scheduling behavior.
- [Scaffolding](./scaffolding) creates a starting configuration for an existing monorepo.
- [Terrabuild Insights](../insights/index.md) adds a shared delivery record and engineering signals.
- [Troubleshooting](/docs/troubleshooting) and `terrabuild explain` help investigate unexpected selection or execution.
