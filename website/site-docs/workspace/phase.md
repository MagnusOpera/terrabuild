---
title: Phase Block

---

A phase is an optional, workspace-wide execution boundary. It is useful when a group of targets must finish before targets in another group can start, even when those targets use different names or belong to different projects.

For example, a workspace can build toolchain images before running application targets that use those images as extensions.

Phases are declared only in `WORKSPACE`. Targets in either `WORKSPACE` or `PROJECT` can reference them.

## Example

```hcl {filename="WORKSPACE"}
phase toolchains {}

phase application {
  depends_on = [ phase.toolchains ]
}

target build {
  phase = phase.application
}
```

A project can assign a different target to the prerequisite phase:

```hcl {filename="tools/pnpm/PROJECT"}
project pnpm {
  @shell {}
}

target dist {
  phase = phase.toolchains
  @shell docker {
    args = "build --tag pnpm-toolchain:${terrabuild.version} ."
  }
}
```

When an application `build` target is selected, Terrabuild enlists `pnpm:dist` because `application` depends on `toolchains`. The toolchain target must complete successfully before the application target starts.

## Selection and Execution Rules

* Selecting a target assigned to a phase enlists every target assigned to all transitive prerequisite phases.
* Prerequisite targets are enlisted even when they are outside an explicit project filter. Their ordinary target dependencies are enlisted as well.
* Other targets in the selected target's own phase are not automatically selected.
* Targets within one phase may run concurrently, subject to their normal target dependencies.
* Every enlisted target in prerequisite phases must complete successfully before a downstream phase starts. A failure prevents downstream targets from running.
* Empty intermediate phases preserve transitive ordering.
* Unphased targets keep their existing dependency and scheduling behavior.
* Phase dependencies and ordinary target dependencies are validated together. Circular combinations are rejected before execution.

Phases affect scheduling but do not become artifact-cache inputs. Assigning a phase does not, by itself, change a target's artifact hash.

## Target Assignment and Inheritance

A workspace target can provide a default phase for every matching project target:

```hcl {filename="WORKSPACE"}
target build {
  phase = phase.application
}
```

A project target can inherit that phase, select another declared phase, or explicitly opt out:

```hcl {filename="PROJECT"}
target build {
  phase = nothing
  @shell echo { args = "unphased build" }
}
```

The `phase` attribute is optional. Existing targets remain unphased when neither the workspace target nor the project target assigns one.

Within target expressions, [`terrabuild.phase`](../expression/predefined-variables) contains the assigned phase name or evaluates to `nothing` for an unphased target.

## Batching and Graphs

Batch builds never mix targets from different phases, and phased targets are never batched together with unphased targets. Compatible targets in the same phase can still be batched normally.

Generated Mermaid graphs group phased targets into subgraphs and show phase ordering with dashed phase edges.

## Argument Reference

The following arguments are supported:

* `identifier` - (Mandatory) Unique phase name within `WORKSPACE`.
* `depends_on` - (Optional) List of prerequisite phase references using `phase.<name>`. Phase dependencies must form an acyclic graph.
