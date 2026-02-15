# Terrabuild Usage Skill

This guide focuses on using Terrabuild as a workspace user.

## What Terrabuild Does

Terrabuild reads your `WORKSPACE`, computes the dependency graph, and executes targets (`install`, `build`, `test`, `dist`, etc.) in the right order with parallelism.

## Core Concepts

- `WORKSPACE`: top-level workspace definition.
- `target`: lifecycle stage to run (for example `build`, `test`).
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

- `terrabuild-debug.config.json`
- `terrabuild-debug.node.json`
- `terrabuild-debug.action.json`
- `terrabuild-debug.batch.json`
- `terrabuild-debug.cascade.json`
- `terrabuild-debug.info.md`

## Workspace Usage Tips

- Keep target names consistent (`install`, `build`, `test`, `dist`).
- Use `depends_on` to model build order.
- Put common defaults in extension definitions.
- Use `locals` for environment-specific values.
