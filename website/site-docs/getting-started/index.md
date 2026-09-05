---
title: Start here
description: Learn Terrabuild through a small workflow, then apply it to your repository.
---

Terrabuild brings **desired-state configuration** to build and deployment.
You declare the result you want and what must be true for it to hold. Terrabuild
coordinates the tools, project dependencies, and environment settings needed to
reach that result, reusing valid work according to your policies.

## Learn by doing

Follow this path if you are new to Terrabuild:

| Step | What you will learn |
| --- | --- |
| [1. Understand the model](./how-it-works.md) | How projects, targets, and extensions fit together. |
| [2. Install Terrabuild](./install.md) | Get the CLI working on your machine. |
| [3. Run your first workflow](./quick-start.md) | Write two small projects, follow dependencies, and restore cached files. |
| [4. Use your existing tools](./existing-repository.md) | Move a familiar build into Terrabuild without rewriting it. |
| [5. Configure environments](./environments.md) | Pass settings deliberately and keep reusable outputs independent of deployment context. |
| [6. Customize a tool](./customization.md) | Configure an included extension or write your own with FScript. |

## Grow the workflow

[Model a deployment](./deployment.md) connects application artifacts to an environment
and explains the difference between reusable files and actions that change an
environment. [Advanced scenarios](./advanced-scenarios.md) covers generated code,
workspace toolchains, independent applications, and CI adoption.

[Insights](../insights/index.md) adds shared artifacts and delivery history when
you need them. You can complete the tutorials using only Terrabuild's local cache.

## Find a precise answer

Use **Concepts and policies** to understand graph selection, execution, caching,
and batching. Use **Reference** for configuration attributes, CLI options, and
extension actions. [Troubleshooting](../troubleshooting.md) helps explain unexpected results.

Still evaluating? [Why Terrabuild](./why-terrabuild.md) describes where it helps
and how its responsibilities fit alongside your existing tools and CI.
