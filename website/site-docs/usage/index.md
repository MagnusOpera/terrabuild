---
title: Usage
---

Terrabuild is used through its command-line interface.

If you need help for any command, append `--help`:

```text
USAGE: terrabuild [--help] [version] [<subcommand> [<options>]]

SUBCOMMANDS:

    scaffold <options>    Scaffold workspace.
    logs <options>        dump logs.
    run <options>         Run specified targets.
    impact <options>      Report impacted targets compared to a base commit.
    serve <options>       Serve specified targets.
    console <options>     Launch web console.
    clear <options>       Clear specified caches.
    prune <options>       Prune stale local build cache entries.
    login <options>       Connect to backend.
    logout <options>      Disconnect from backend.

    Use 'terrabuild <subcommand> --help' for additional information.
```

## Command Pages

- [run](./run): build or restore selected targets
- [impact](./impact): compare the current graph with a base commit
- [logs](./logs): read stored build logs
- [serve](./serve): serve selected projects
- [scaffold](./scaffold): create starter `WORKSPACE` and `PROJECT` files
- [console](./console): launch the local web console
- [clear](./clear): clear local caches
- [prune](./prune): remove stale local cache entries
- [login](./login): connect to Insights
- [logout](./logout): remove a saved Insights connection

## Common Patterns

- Use `terrabuild run <target>` for normal local and CI builds.
- Use `terrabuild run <target> --out <file>` when automation needs a machine-readable build report.
- Use `terrabuild impact <target> --base <sha> --out <file>` to compute impacted targets from a stored base graph.
- Use `terrabuild logs <target>` when you only need the stored logs for a target.
- Use `terrabuild login` once per machine or CI environment to enable shared cache and Insights-backed features.
