# Terrabuild Usage Skill

This guide focuses on using Terrabuild as a workspace user.

## What Terrabuild Does

Terrabuild reads your `WORKSPACE`, computes the dependency graph, and executes targets (`install`, `build`, `test`, `dist`, etc.) in the right order with parallelism.

## Core Concepts

- `WORKSPACE`: top-level workspace definition.
- `target`: lifecycle stage to run (for example `build`, `test`).
- `phase`: an optional workspace-declared ordering boundary spanning multiple targets.
- project: a repository unit matched by workspace rules.
- extension: reusable command provider (for example `@dotnet`, `@pnpm`, `@docker`, `@terraform`).

## Daily Commands

Show help:

```bash
terrabuild --help
terrabuild run --help
terrabuild logs --help
```

Run one target:

```bash
terrabuild run build
```

Run multiple targets:

```bash
terrabuild run build test
```

Run with explicit context:

```bash
terrabuild run build --workspace . --configuration local --environment dev
```

Force rerun and keep execution logs:

```bash
terrabuild run build --force --log
```

Run locally only:

```bash
terrabuild run build --local-only
```

Tune parallelism:

```bash
terrabuild run build --parallel 4
```

Replay logs for targets:

```bash
terrabuild logs build test --log
```

## Useful Debug Mode

When execution is unclear, run with debug output:

```bash
terrabuild run build --debug --log --force
```

Useful generated files:

- `terrabuild-debug.options.json`
- `terrabuild-debug.config.json`
- `terrabuild-debug.full-node.json`
- `terrabuild-debug.node.json`
- `terrabuild-debug.resolve.json`
- `terrabuild-debug.action.json`
- `terrabuild-debug.cascade.json`
- `terrabuild-debug.batch.json`
- `terrabuild-debug.info.md`

## Workspace Usage Tips

- Keep target names consistent (`install`, `build`, `test`, `dist`).
- Use `depends_on` to model build order.
- Put common defaults in extension definitions.
- Use `locals` for environment-specific values.

## Optional Build Phases

Declare phases in `WORKSPACE` when a group of targets must finish before another group can start:

```hcl
phase toolchains {}

phase application {
  depends_on = [phase.toolchains]
}

target build {
  phase = phase.application
}
```

Both workspace and project targets can reference a phase, but `phase` blocks themselves are valid only in `WORKSPACE`. A project target inherits the phase on its matching workspace target. Use `phase = nothing` in the project target to opt out.

Selecting a phased target enlists all targets assigned to its transitive prerequisite phases, along with their normal dependencies. It does not enlist unrelated targets in its own phase. Targets without a phase keep normal dependency behavior.

Targets in one phase may run concurrently, but no target in a downstream phase starts until every enlisted prerequisite-phase target succeeds. Batch execution never combines targets from different phases or combines phased and unphased targets. Markdown graphs remain ungrouped; phase grouping is an opt-in view in the interactive console.

Inside target expressions, `terrabuild.phase` contains the assigned phase name. It evaluates to `nothing` for an unphased target.
