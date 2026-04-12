---
title: Release Candidate 1
date: 2026-03-04
excludeSearch: true
tags:
  - Terrabuild
---

# Terrabuild RC1: Security, Artifacts, and a New Execution Core

The last Terrabuild beta was released on **September 30th, 2024**.

Since then, a lot has changed.

What started as incremental improvements eventually turned into a deeper evolution of Terrabuild’s architecture: stronger security guarantees, a redesigned execution model, a more powerful scripting system, and a new embedded console experience.

Over the past months, Terrabuild has evolved far beyond incremental fixes. Large parts of the execution engine, scripting system, and artifact model were redesigned to improve security, performance, and extensibility.

Today I’m happy to introduce **Terrabuild RC1** (release `0.189.22`).

This release focuses on five areas:

- **Security**
- **Artifacts and caching**
- **Batch execution**
- **Graph execution engine**
- **Scripting and language evolution**

---

# Security First

One of the most important changes in this cycle is a stronger security model.

Terrabuild now supports **client-side encryption of artifacts before they are stored on the server**. This means that build artifacts can be uploaded to remote storage **without the server ever being able to read them**.

The encryption key never leaves the client environment.

This approach significantly reduces the trust surface: storage infrastructure can remain simple while sensitive artifacts stay protected.

Other security improvements include:

### Scripting hardening

- Workspaces can define **deny glob patterns**
- Combined with FScript (see below), extension execution is now safer.

### Operational safety improvements

- Stronger error propagation
- Automatic tracking and termination of spawned processes
- Stricter failure handling during graph execution

These changes aim to make Terrabuild safer both in **local development environments and CI systems**.

---

# From Cache to Artifacts

Terrabuild originally relied on a classic *build cache* model.

During this cycle, that model evolved toward something more explicit: **artifacts**.

Artifacts represent **materialized build outputs with clear ownership and lifecycle**. They can exist locally or remotely, and Terrabuild now manages them at multiple levels:

- **Target-level artifacts**
- **Extension-level artifacts**
- **Workspace artifact caches**

The system also introduces:

- Managed artifacts
- External cache policies
- Better remote cache upload/download behavior
- Improved restore vs rebuild correctness

This shift clarifies an important distinction: Terrabuild does not simply cache results — it **manages build artifacts as first-class entities**.

---

# Batching Revisited

Batching returned in this cycle, but it did not come back unchanged.

The implementation was **reintroduced and then completely redesigned**.

Terrabuild now supports:

- Proper **batch computation**
- Atomic **batch progress tracking**
- Consistent batch logs and reporting
- Improved CI and local execution parity

Batching is also now **dynamic for scripting commands**.

Instead of declaring batching capabilities statically, Terrabuild can infer them **from command results**, allowing scripts to participate naturally in batched builds.

This significantly improves performance when building large dependency graphs.

---

# A New Graph Execution Core

Perhaps the biggest internal change is the **execution engine rewrite**.

Terrabuild’s graph builder and scheduler were significantly refactored around a **continuation-based execution model**.

This enabled several improvements.

## Smarter dependency evaluation

The engine now performs:

- Cascade and cluster computation
- Required-node fast paths
- Skipping of non-required nodes
- Delegated restore logic

This reduces unnecessary work in large graphs.

## Better scheduling

The runtime now supports:

- Prioritized scheduling
- Background continuations for IO
- Immediate scheduling stop on first error

## Stronger concurrency guarantees

Several reliability improvements were made to the hub/pubsub system:

- Lock improvements
- Deadlock fixes
- Safer node status handling
- Stricter failure propagation

Overall, these changes make the engine **more predictable under load** while also improving performance on complex builds.

---

# The Web Console

Terrabuild now includes an **embedded web console directly inside the executable**.

It provides:

- A **graph visualization**
- A **terminal interface**
- Build parameters and metadata panels
- Cache inspection tools
- Notifications and live progress updates

The console aims to provide better **runtime visibility** without requiring external tooling.

Other improvements include:

- Human-readable durations
- Clearer progress rendering
- Richer batch output
- Interactive graph nodes and edges

---

# The Rise of FScript

Another major change is the migration toward **FScript-based scripting and extensions**.

Legacy extension projects have progressively been replaced by scripts, which allows:

- Faster iteration
- Easier extension development
- Safer execution boundaries

FScript is an interpreted language, allowing safe sandboxed execution of extensions.

Scripts are of course available if you want to implement your own extensions for custom behavior or integrations.

More information about FScript can be found at: https://magnusopera.github.io/FScript/

---

# Toward Release

This release candidate represents more than a set of improvements.

It reflects a gradual shift in Terrabuild’s philosophy:

- **Artifacts instead of cache**
- **Scripts instead of extension projects**
- **Continuation-based execution instead of simple task scheduling**
- **Client-side security instead of server trust**

Terrabuild remains focused on the same goal:

> Making build systems powerful without making them complex to maintain.

If you have been using Terrabuild since the beta, this release should feel **more robust, safer, and significantly more capable**.

RC1 marks the point where these architectural changes stabilize and move toward a final release.