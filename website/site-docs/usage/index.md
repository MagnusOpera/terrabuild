---
title: Usage
---

Use the Terrabuild CLI to run targets, inspect build decisions, compare changes, and manage your local workspace.

## Choose a command

| Task | Command | When to use it |
| --- | --- | --- |
| Build, test, package, or deploy | [run](./run) | Execute one or more targets and their prerequisites; optionally restore cached outputs or write a JSON result. |
| Start development services | [serve](./serve) | Run the `serve` target for selected projects. |
| Read previous build logs | [logs](./logs) | Inspect stored target logs without starting another build. |
| Understand the next run | [explain](./explain) | See selection, cache, and scheduling decisions without executing targets. |
| Measure branch impact | [impact](./impact) | Compare the local graph with a base commit graph stored in Insights and write changed targets to JSON. |
| Investigate cache-key changes | [compare](./compare) | Compare two saved diagnostic reports locally, including changed files, dependencies, and inputs. |
| Create workspace configuration | [scaffold](./scaffold) | Generate starter `WORKSPACE` and `PROJECT` files. |
| Explore the graph visually | [console](./console) | Open the local web console. |
| Reset local caches | [clear](./clear) | Remove selected local cache data. |
| Reclaim cache space | [prune](./prune) | Remove local cache entries older than an access-age threshold. |
| Connect shared cache and Insights | [login](./login) | Save an Insights workspace connection on this machine or CI runner. |
| Remove an Insights connection | [logout](./logout) | Delete a saved connection. |
| Check the installed binary | [version](./version) | Print the installed Terrabuild version. |

`explain` answers what a run would do now. `impact` answers which logical targets changed since a recorded commit. `compare` answers why cache keys or decisions differ between two saved runs. Only `impact` requires an Insights baseline.

## Get command help

Append `--help` to any command, for example `terrabuild impact --help`. Run `terrabuild --help` for the top-level usage:

```text
USAGE: terrabuild [--help] [version] [<subcommand> [<options>]]

SUBCOMMANDS:

    scaffold <options>    Scaffold workspace.
    logs <options>        dump logs.
    run <options>         Run specified targets.
    compare <options>     Explain cache-key changes between two saved diagnostic reports.
    explain <options>     Explain target selection and execution decisions.
    impact <options>      Report impacted targets compared to a base commit.
    serve <options>       Serve specified targets.
    console <options>     Launch web console.
    clear <options>       Clear specified caches.
    prune <options>       Prune stale local build cache entries.
    login <options>       Connect to backend.
    logout <options>      Disconnect from backend.

    Use 'terrabuild <subcommand> --help' for additional information.
```

## Shared selection options

`run`, `explain`, `impact`, `logs`, and `serve` accept the following options. Each command page lists its complete syntax and additional options.

| Option | Short form | Purpose |
| --- | --- | --- |
| `--workspace <path>` | `-w` | Use this workspace root. When omitted, Terrabuild searches the current directory and its parents for `WORKSPACE`. |
| `--configuration <name>` | `-c` | Select a configuration, overriding the workspace default. |
| `--environment <name>` | `-e` | Select the environment used to evaluate configuration and targets. |
| `--variable <name>=<value>` | `-v` | Set a variable. Repeat the option to set multiple variables. |
| `--project <projects>...` | `-p` | Select projects by identifier or workspace-relative path. Unknown selectors fail the command. |
| `--label <labels>...` | `-l` | Select projects matching any of the supplied labels. |
| `--type <types>...` | `-t` | Select projects using any of the supplied extension types. |

Different filters are combined: a project must satisfy each supplied filter. Dependencies required by the selected targets can include projects outside those filters.

```bash
terrabuild run build test -w ./repo -p api web -c Release -e staging -v version=1.2.3
```

`scaffold` and `console` also accept `--workspace` (`-w`); see their pages for workspace behavior. For `login`, `--workspace` is an Insights workspace ID, not a filesystem path.

## Common patterns

- Use `terrabuild run <target>` to build, test, package, or deploy locally and in CI.
- Use `terrabuild explain <target>` to understand what Terrabuild would do and why.
- Use `terrabuild run <target> --out <file>` when automation needs a machine-readable build report.
- Use `terrabuild impact <target> --base <sha> --out <file>` to compute impacted targets from a stored base graph.
- Use `terrabuild compare --before <report.json> --after <report.json>` to explain cache-key changes between saved debug runs without an Insights connection.
- Use `terrabuild logs <target>` when you only need the stored logs for a target.
- Use `terrabuild login` once per machine or CI environment to enable shared cache and Insights-backed features.
