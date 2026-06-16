---
title: Usage

---

Terrabuild's interface is the CLI (Command Line Interface). If you need help with any command, just append `--help` and detailed information will be provided.
```
> terrabuild --help
USAGE: terrabuild [--help] [version] [<subcommand> [<options>]]

SUBCOMMANDS:

    scaffold <options>    Scaffold workspace.
    logs <options>        dump logs.
    run <options>         Run specified targets.
    serve <options>       Serve specified targets.
    clear <options>       Clear specified caches.
    prune <options>       Prune stale local build cache entries.
    login <options>       Connect to backend.
    logout <options>      Disconnect from backend.

    Use 'terrabuild <subcommand> --help' for additional information.

OPTIONS:

    version               Show current Terrabuild version.
    --help                display this list of options.
```

## Run Target
Command to run one or more targets. This is the primary command for building your workspace:
```
> terrabuild run --help
USAGE: terrabuild run [--help] [--workspace <path>] [--configuration <name>] [--environment <name>] [--variable <variable>=<value>] [--label [<labels>...]] [--type [<types>...]]
                      [--project [<projects>...]] [--force] [--retry] [--parallel <max>] [--local-only] [--note <note>] [--tag <tag>] [--engine <engine>] [--what-if] [--result <path>] <target>...

TARGET:

    <target>...           Specify build target.

OPTIONS:

    --workspace, -w <path>
                          Root of workspace. If not specified, current directory is used.
    --configuration, -c <name>
                          Configuration to use.
    --environment, -e <name>
                          Environment to use.
    --variable, -v <variable>=<value>
                          Set variable.
    --label, -l [<labels>...]
                          Select projects based on labels.
    --type, -t [<types>...]
                          Select projects based on extension types.
    --project, -p [<projects>...]
                          Select projets base on id.
    --force, -f           Ignore cache when building target.
    --retry, -r           Retry failed task.
    --parallel <max>      Max parallel build concurrency (default to number of processors).
    --local-only          Use local cache only.
    --note <note>         Note for the build.
    --tag <tag>           Tag for build.
    --engine <engine>     Container engine to use (docker, podman or none).
    --what-if             Prepare the action but do not apply.
    --result <path>       Write machine-readable result JSON to file.
    --help                display this list of options.
```

### Machine-readable Result Report

Use `--result <path>` to write a JSON report describing the final outcomes of a run.

Example:

```bash
terrabuild run build --result artifacts/run-result.json
```

The report uses `lowercase(ProjectName):target` as the public identifier.
Projects without an assigned `name` are intentionally omitted from the report.

```json
{
  "status": "success",
  "targets": ["build"],
  "startedAt": "2026-04-11T16:42:46.595161Z",
  "endedAt": "2026-04-11T16:43:28.917020Z",
  "results": {
    "app:build": "success",
    "lib:test": "ignored"
  }
}
```

Possible result values are:

- `success`: At least one matching node completed successfully and none failed.
- `failure`: At least one matching node failed.
- `ignored`: The named node existed in the graph but produced no runner result.

## Clear Local Cache
Command `clear` allows you to clear local cache. This is useful when you want to force a complete build or free up disk space:
```
> terrabuild clear --help
USAGE: terrabuild clear [--help] [--cache] [--home] [--temporary] [--all]

OPTIONS:

    --cache               Clear build cache.
    --home                Clear home cache.
    --temporary           Clear tmp cache.
    --all                 Clear all caches.
    --help                display this list of options.
```

## Prune Local Cache
Command `prune` removes stale entries from `~/.terrabuild/cache` while leaving `~/.terrabuild/home` and `~/.terrabuild/tmp` untouched:
```
> terrabuild prune --help
USAGE: terrabuild prune [--help] <days>

TARGET:

    <days>                Prune local build cache entries not accessed for more than <days> days.

OPTIONS:

    --help                display this list of options.
```

Example:
```
> terrabuild prune 7
```

Freshness is based on Terrabuild-managed access timestamps on each cache entry `origin` file, not filesystem last-access (`atime`) behavior.

## Connect to a Shared Cache
The `login` command lets you connect to a shared cache (Insights workspace), enabling faster builds by sharing artifacts between machines and CI/CD pipelines. All artifacts are encrypted on the client side and never stored unencrypted.
```
> terrabuild login --help
USAGE: terrabuild login [--help] --workspace <id> --token <token> --masterkey <masterKey>

OPTIONS:

    --workspace <id>      Workspace Id to connect to
    --token <token>       Token to connect to space
    --masterkey <masterKey>
                          Master key to encrypt artifacts
    --help                display this list of options.
```
