---
title: Terrabuild Insights
description: Understand the hosted delivery record that complements Terrabuild.
slug: /insights
---

Terrabuild converges a declared delivery graph toward the requested state.
Insights is the hosted record of the states that were reached.

When a workspace is connected, Terrabuild sends the execution context, selected
graph, task outcomes, and artifact metadata to Insights. Insights keeps those
runs together across developer machines and CI, then combines them with GitHub
history to show how changes move through the delivery system.

Terrabuild remains usable without Insights. Its local graph, execution engine,
and cache do not require an account.

## What Insights adds

### A shared operational record

See what ran, where it ran, who started it, and whether it completed. Related
Terrabuild commands can share a group so that a build-and-plan invocation and a
later approved deployment appear as one delivery.

Each run retains its task graph. Open it to inspect selected and ignored nodes,
dependencies, phases, cache decisions, artifacts, and execution results.

### Environment history

Insights follows successful delivery targets in configured environments. The
timeline shows which project versions and commits reached preview, staging, or
production, including forward changes, rollbacks, and divergent histories.

### Release boundaries

Define which targets and environments represent a release. Insights uses those
boundaries to compare release points and assemble introduced or removed commits
into release notes.

### Engineering signals

Pulse combines Terrabuild executions with GitHub pull requests, reviews, and
comments. It presents questions about flow, feedback, delivery, involvement,
and recurring behavior, with the underlying evidence available for inspection.

### Shared encrypted artifacts

Targets with managed artifacts can publish encrypted outputs to Insights.
Another authorized machine can restore the same result when Terrabuild computes
the same cache identity. Encryption and decryption happen on the Terrabuild
machine with the workspace master key.

## Product boundary

| Terrabuild | Insights |
| --- | --- |
| Reads `WORKSPACE` and `PROJECT` files | Receives execution graphs and outcomes |
| Selects and orders targets | Keeps delivery history across machines and CI |
| Executes or restores tasks | Presents environment and release timelines |
| Manages the local cache | Stores encrypted shared artifacts |
| Runs with existing build and deployment tools | Adds GitHub context and engineering signals |

Insights observes and explains Terrabuild delivery. It does not become the
execution engine, and a temporary Insights outage does not erase the completed
local cache entry.

## Continue

- [The delivery record](./delivery-record.md) explains runs, groups, graphs, and artifacts.
- [Environments and releases](./environments-and-releases.md) covers timelines and release notes.
- [Pulse](./pulse.md) describes the engineering questions and evidence model.
- [Connect Insights](../getting-started/insights.md) configures a workspace and shared artifacts.
