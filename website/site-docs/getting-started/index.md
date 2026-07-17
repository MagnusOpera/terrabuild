---
title: Getting Started

---

This section takes you from a first build to the mental model needed to configure and troubleshoot a real workspace.

## Your Learning Journey

Follow the pages in this order:

1. **Install** Terrabuild and run the playground.
2. **Learn the core model**: workspace, project, target, task, dependency, and phase.
3. **Scaffold your repository** and adapt the generated configuration.
4. **Understand execution** through the graph, cache, and task-action model.
5. **Optimize later** with batch builds.

Understanding these concepts will help you configure Terrabuild effectively and troubleshoot issues when they arise.

Terrabuild turns project targets into one immutable task graph. It then decides for each selected node whether to execute it, restore its artifacts, or report a previous failed result. Extensions keep your existing build tools in charge of the actual work.

## What You'll Learn

As you progress through this section, you'll understand:

- How Terrabuild discovers projects and creates a task graph
- How project, target, and phase dependencies differ
- How caching works and why it makes builds fast
- How tasks execute and when they build vs restore
- How to configure your workspace and projects
- When phases or batch operations are appropriate

By the end, you'll have a solid understanding of how Terrabuild works and how to use it effectively in your projects.

- [Install](/docs/getting-started/install): Install the CLI
- [Quick Start](/docs/getting-started/quick-start): Run a working example
- [Key Concepts](/docs/getting-started/key-concepts): Build the mental model
