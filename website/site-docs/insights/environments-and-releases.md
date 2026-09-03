---
title: Environments and releases
description: Follow deployed changes, rollbacks, divergence, and release boundaries in Insights.
---

A list of successful builds does not answer what is running in an environment.
Insights derives an environment timeline from successful Terrabuild delivery
targets and their submitted graphs.

## Configure a release profile

A release profile identifies the repository, environment, and Terrabuild target
that mark a meaningful delivery point. A workspace can define separate profiles
for applications or release lanes that share one monorepo.

Examples include:

- an application deployed to staging by `apply`;
- the same application deployed to production from a release tag;
- another application with an independent production cadence.

Configure release rules before using environment timelines, release notes, or
delivery-frequency metrics.

## Read the environment timeline

For each delivery point, Insights compares the submitted graph with the previous
visible point in the same profile. It shows:

- the deployed branch, tag, and head commit;
- the actor, time, and Terrabuild targets;
- projects added, changed, or removed from the graph;
- commits introduced into or removed from the environment;
- the Terrabuild runs that established the point.

The history classification provides a quick warning:

| State | Meaning |
| --- | --- |
| Initial deployment | The first visible point for the profile. |
| Forward change | The new revision follows the previous revision. |
| Rollback | The environment returned to an earlier revision. |
| Divergent history | The two revisions do not form a direct forward or rollback path. |

Commit history may be incomplete while GitHub enrichment is still collecting
ancestry. Insights marks that condition instead of presenting partial history as
complete.

## Generate release notes

Release Notes compares two points in a release profile. It uses GitHub history
to list commits introduced and removed between those boundaries.

This is graph-aware release history rather than a list of commits made during a
date range. The boundaries come from observed Terrabuild deliveries to the
configured environment.

Generated notes can be copied as Markdown and then edited for the intended
audience. Insights supplies the delivery facts; it does not infer product
language or hide changes automatically.

## Multiple applications in one repository

Use a separate release profile when applications have independent delivery
targets or environments. A shared repository does not imply one release stream.

For example, a Magnus Opera monorepo may have `studio` and `catalog`
applications with separate staging and production targets. Their builds can
share libraries and toolchains while their release timelines remain independent.
