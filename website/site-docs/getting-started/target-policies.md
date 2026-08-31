---
title: Target policies
prev: /docs/getting-started/tasks
next: /docs/getting-started/caching
---

A target policy tells Terrabuild how one named kind of work participates in the graph. Commands describe **what** a project does; target attributes describe **when** it is selected, **what** must precede it, **whether** its result can be reused, and **how** concurrent work may be grouped.

Put shared policy in `WORKSPACE` so every project target with the same name behaves consistently. Use a target block in `PROJECT` for its commands and only the exceptions that are specific to that project.

## Start with the questions

Choose target attributes by answering these questions in order:

| Question | Attribute | Typical answer |
|----------|-----------|----------------|
| Which specific tasks must finish first? | `depends_on` | Build upstream libraries before the application. |
| Must an entire class of workspace work finish first? | `phase` | Build all local toolchains before any application work starts. |
| When should this target execute, and should that execution propagate? | `build` | Execute an ordinary build only when its inputs or dependencies changed. |
| Which generated files belong to the result? | `outputs` | Preserve `dist/**` or generated source files. |
| Who owns and restores the result? | `artifacts` | Terrabuild, Insights, an external registry, or nobody. |
| May compatible project tasks share a native invocation? | `batch` | Build a connected .NET partition through one generated solution. |
| Does deployment context intentionally change the result? | `environment_sensitive` | Hash the selected staging or production environment. |
| Does the target mutate a shared machine-global resource? | `lock` | Serialize a generator that installs or updates shared NuGet tools. |

You do not need to set every attribute. Terrabuild infers outputs and artifact behavior from commands where possible, uses `build = ~auto` and `batch = ~single` by default, treats targets as environment-neutral, and applies no phase or named lock unless configured.

## A normal build policy

This workspace policy describes application builds that follow the project dependency graph, run after local toolchains, share managed outputs, and batch only dependency-connected groups:

```hcl {filename="WORKSPACE"}
phase toolchains { }

phase application {
  depends_on = [ phase.toolchains ]
}

target build {
  phase = phase.application
  depends_on = [ target.^build ]
  build = ~auto
  artifacts = ~managed
  batch = ~partition
  environment_sensitive = false
}
```

Each line answers a different question:

- `phase` creates a workspace-wide success barrier after all enlisted toolchain targets.
- `target.^build` follows actual upstream project dependencies.
- `~auto` executes when the cache identity changed or a non-lazy prerequisite executed.
- `~managed` makes declared outputs portable through Insights while retaining local reuse.
- `~partition` avoids combining disconnected project components into an oversized batch.
- `false` declares that build output must not depend on branch, tag, CI state, or deployment environment.

These values are an example policy, not universal defaults. A small workspace may prefer the default `~single` batch, and a workspace that never shares outputs may prefer `~workspace` artifacts.

## Choose dependencies and phases

Use `depends_on` for a concrete data or execution requirement:

```hcl
target build {
  depends_on = [ target.^build ]
}

target dist {
  depends_on = [ target.build ]
}
```

`target.^build` means `build` on upstream dependency projects. `target.build` means `build` on the current project. A reference adds only targets that exist in the relevant project scope.

Use a `phase` when the invariant applies to a whole workspace category. A prerequisite phase is a success barrier: selecting a downstream phased target enlists every target in the prerequisite phase, and none of the downstream phase starts until all enlisted work succeeds.

Do not use a broad phase when one target needs one producer. Prefer a direct target or project dependency in that case so Terrabuild can keep unrelated work outside the selected graph. See [Phases](../workspace/phase) for the complete selection and inheritance rules.

## Choose a build mode

| Mode | Use it when | Important consequence |
|------|-------------|-----------------------|
| `~auto` | The target is a deterministic build, test, package, or plan whose declared identity describes its result. | It executes on a cache miss or propagated dependency execution and otherwise reuses cache. |
| `~always` | Every invocation must perform a side effect or observe mutable external state, such as applying a deployment. | It ignores a matching cache entry and is always required. Usually pair it with `artifacts = ~none`. |
| `~lazy` | The target prepares something needed only by executing consumers, such as local setup or a generator, and its execution must not force otherwise reusable dependents to rebuild. | An explicitly selected lazy target still executes on a cache miss. Laziness affects dependency realization and rebuild propagation, not priority. |

Ordinary dependency execution propagates to dependents unless the dependency is lazy. Phase barriers enforce ordering and success but do not by themselves propagate execution into downstream targets with valid cache entries.

## Choose outputs and artifact ownership

`outputs` identifies the files and directories Terrabuild may preserve and replace during restoration. If it is omitted, Terrabuild combines outputs declared by the project and the commands used by the target. Override it when those inferred patterns are incomplete or too broad.

```hcl
target dist {
  outputs = [ "dist/**" ]
}
```

Then choose who owns the reusable result:

| Mode | Choose it for | Example |
|------|---------------|---------|
| `~none` | Work that must not retain a reusable summary or files. | Applying infrastructure, starting a development server, or another side effect. |
| `~workspace` | Files that should be reusable on this machine but do not need to be shared. | Large local generated sources, private intermediate output, or an offline developer build. |
| `~managed` | Files that developers and CI should share through encrypted Insights storage. | Compiled assemblies, application bundles, or distributable archives. |
| `~external` | The command publishes or maintains the artifact in another system. Terrabuild retains only the execution summary and never restores files. | A Docker image in a registry or a package already published to NuGet or npm. |

Artifact ownership and build mode are separate. `artifacts = ~external` can reuse a successful summary even though the actual image or package remains outside Terrabuild. `build = ~always` bypasses that summary and executes again.

## Choose a batch mode

Batching is available only when every resolved command supports it and the extension assigns compatible tasks to the same cluster.

| Mode | Choose it when |
|------|----------------|
| `~single` | One native invocation across all required compatible tasks is efficient and safe. This is the default. |
| `~partition` | Disconnected components should be isolated to reduce batch size and failure scope while preserving native batching inside each component. |
| `~never` | The tool is unsafe in a shared invocation, project-specific isolation matters, or you are diagnosing a batching problem. |

See [Batch](/docs/getting-started/batch) for eligibility, cache interaction, and concrete examples.

## Opt into environment-sensitive inputs deliberately

Targets are environment-neutral by default. A neutral target fails before execution if it consumes a sensitive predefined value such as the selected environment, branch, tag, or CI state.

Set `environment_sensitive = true` only when that contextual value intentionally changes the output:

```hcl
target plan {
  environment_sensitive = true
}
```

The consumed sensitive value hashes then participate in the cache key. This is appropriate for a staging-specific Terraform plan or an environment-tagged image. It is usually a mistake for compilation that should be reusable across branches and environments.

## Serialize shared machine state with a named lock

Independent graph nodes normally execute concurrently. Add a named lock when they mutate the same resource outside their project directories:

```hcl
target generate {
  lock = "nuget-tools"
}
```

The same name coordinates threads, batches, and concurrent Terrabuild processes on the machine. The lease covers execution and restoration of declared outputs, and the operating system releases ownership after exceptions or process termination. It is not a replacement for graph dependencies: use `depends_on` when one task consumes another task's result.

A workspace target supplies the default lock for matching project targets. A project may replace it or use `lock = nothing` to opt out.

## Common policy recipes

### Externally managed image

```hcl
target image {
  build = ~auto
  artifacts = ~external
  depends_on = [ target.build ]
}
```

Terrabuild can reuse the successful Docker build summary, but the image remains in the Docker registry and no files are restored into the project.

### Side-effecting deployment

```hcl
target deploy {
  build = ~always
  artifacts = ~none
  depends_on = [ target.plan ]
  environment_sensitive = true
}
```

Every selected deployment executes after its plan, retains no reusable result, and may intentionally depend on the selected environment.

### Shared generator or toolchain

```hcl
target generate {
  phase = phase.toolchains
  build = ~lazy
  artifacts = ~managed
  lock = "nuget-tools"
}
```

The generator belongs to the toolchain phase, can share its declared files, does not propagate its execution into reusable dependents, and cannot collide with another process using the same machine-global tool area.

## Verify the decision before running

Use `explain` with the same filters and variables as the intended run:

```bash
terrabuild explain build
terrabuild explain deploy --environment staging
```

For each selected target, verify the dependencies, assigned phase, action and reason, artifact mode, outputs, batch membership, environment-sensitive inputs, named locks, and final scheduling outcome. See the [`explain` command](../usage/explain) for details.
