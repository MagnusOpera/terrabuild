---
title: Get started
description: Learn the model, install Terrabuild, and run a complete delivery example.
---

You can understand Terrabuild and run a representative example without first
learning its graph internals or every configuration option.

## 1. Understand the model

[How Terrabuild works](./how-it-works.md) explains desired-state delivery using
projects, outcomes, dependencies, and reusable results. It deliberately avoids
the scheduler and cache implementation details.

## 2. Run the playground

[Install Terrabuild](./install.md), then follow the
[quick start](./quick-start.md). The playground contains shared libraries,
applications, container images, and Terraform, so one small repository can show
the path from a source change to an environment.

## 3. Bring the model to your repository

[Scaffold an existing monorepo](./scaffolding.md) to generate an initial
`WORKSPACE` and `PROJECT` files. Review the result and begin with one useful
outcome, usually `build`, before adding packaging or deployment.

## 4. Add delivery and history when useful

- [Model a deployment](./deployment.md) when application artifacts should feed an environment-specific plan or deployment.
- [Connect Insights](./insights.md) when developer machines and CI should share encrypted artifacts and a delivery record.

The **Deep dive** section is there when you need to tune reuse, understand an
unexpected selection, introduce phases or batching, or inspect the complete
graph model.
