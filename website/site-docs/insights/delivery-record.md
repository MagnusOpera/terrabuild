---
title: The delivery record
description: Read Terrabuild runs, execution graphs, groups, and artifacts in Insights.
---

Insights records Terrabuild activity as a delivery stream rather than a folder
of CI logs. A record carries the repository revision, selected environment,
actor, requested targets, graph, timing, outcome, and associated artifacts.

## Runs and delivery groups

One CI workflow may invoke Terrabuild more than once. For example, protected
deployment can separate planning from application:

```text
build · test · audit · dist · plan
                    ↓ approval
                         apply
```

Pass the same `--group` value to both commands. Insights shows the invocations
under one delivery group while retaining the result and timing of each run.

This distinction matters when deployment approval, credentials, or concurrency
rules require separate CI jobs. The delivery remains connected even though it
did not execute in one process.

## Build graph

Open a run to inspect the graph Terrabuild submitted. The graph view can filter
by target and can include ignored nodes when you need to understand selection.

Selecting a node shows information such as:

- project and target;
- action selected by the cache and propagation rules;
- direct dependencies;
- phase and scheduling policy;
- whether the node executed, restored, reused a summary, failed, or was blocked.

This is often more useful than starting with a long log. It shows why work was
present and what prevented a dependent target from running.

## Execution context

The run record keeps the branch or tag, commit, environment, actor, start and
completion time, and supported CI context. When GitHub Actions context is
available, Insights links the record back to the workflow run.

Environment is a first-class value. A preview deployment and a production
deployment of the same repository revision remain distinct records.

## Artifacts

Insights lists managed artifacts associated with the run, including their
project, target, size, and publication state. Artifact bytes are compressed and
encrypted by Terrabuild before upload.

Externally owned artifacts, such as a container image in a registry, are not
copied into Insights. Terrabuild can still record that the publishing target
completed and reuse its successful summary according to target policy.

## A practical investigation

When a delivery fails:

1. Find the delivery group by repository, branch or tag, and environment.
2. Expand the group to identify the failed Terrabuild invocation.
3. Open its graph and select the failed or blocked target.
4. Follow its dependency edges to confirm the prerequisite chain.
5. Use the linked CI run or Terrabuild logs for command output.

The graph explains the delivery structure; the command log explains the tool
failure.
