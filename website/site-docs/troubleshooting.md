---
title: Troubleshooting

---

This page covers configuration mistakes and inputs Terrabuild cannot discover without help.

## Start with the prepared decision

Run `explain` with the same targets, filters, configuration, environment, variables, cache options, `--force`, and `--retry` flags as the command you are investigating:

```bash
terrabuild explain build
terrabuild explain deploy --environment staging
```

For each node, it reports selection, dependencies, action and reason, cache evidence, evaluated inputs, environment sensitivity, resolved operations, named locks, batch membership, and final scheduling outcome without executing commands. Use a real run with `--debug` only when the question concerns command output, actual timing, or a failure during execution.

See the [`explain` command](./usage/explain) and [target policy guide](./getting-started/target-policies) for the fields and policies behind the decision.

## Target rebuilt unexpectedly

Check the node's action reason in `terrabuild explain <target>`:

| Reason | Why Terrabuild executes |
|--------|--------------------------|
| `forced-cli` | The command uses `--force`. |
| `configured-always` | The target uses `build = ~always`. |
| `dependency-executed` | An ordinary non-lazy prerequisite executes and propagates work. Inspect the reported action dependencies. |
| `non-cacheable` | The target retains no reusable result, commonly because it uses `artifacts = ~none`. |
| `cache-miss` | No summary exists for the computed cache key and permitted cache scope. |
| `cache-outputs-missing` | A successful workspace or managed summary exists, but its declared output archive is unavailable. Terrabuild rebuilds instead of claiming an impossible restore. |
| `retry-failed-cache` | `--retry` replaces a cached failed result with a new execution. |

If the cache key changed, compare the fingerprint inputs between the previous debug report and the current explanation. Check tracked files, dependency hashes, target policy, resolved operations, and evaluated variables rather than assuming the branch or commit itself invalidated the cache.

## Target did not execute

A successful cache hit normally reuses earlier work. Workspace and managed artifacts restore declared files only when a required dependent needs them; an external artifact reuses its successful summary without scheduling file restoration.

Also check the final scheduling outcome. A lazy dependency is not realized behind a dependent that restores or reports a cached failure. An explicitly selected lazy target still executes when its own cache entry is missing.

An outcome of `blocked` means the task did not run because at least one required dependency failed or was itself unable to run. The result message lists the direct blocking node IDs. This is different from `failure`, which means Terrabuild attempted that task and its command, restoration, or summary failed.

Use `--force` only when the same declared inputs must execute again. Use `--retry` when the existing cache entry records failure and should be replaced.

## Expected outputs were not restored

Check all of the following:

- The target uses `artifacts = ~workspace` or `~managed`. `~external` never restores files, and `~none` has no reusable outputs.
- The target's resolved `outputs` patterns include the expected files.
- The node is required by executing work. A successful restore root with no executing dependent does not need runner work.
- The permitted cache scope contains the archive. `--local-only` prevents a managed target from downloading an entry that exists only in Insights.

If the summary exists but the required archive does not, the action reason is `cache-outputs-missing` and Terrabuild executes the target again.

## One target caused dependents to rebuild

Execution propagates through ordinary target dependencies unless the executing prerequisite uses `build = ~lazy`. Use lazy mode for setup or generation that must be available to executing consumers but whose execution should not invalidate otherwise reusable downstream results.

Phase dependencies behave differently: they enforce selection, ordering, and success, but execution in a prerequisite phase does not by itself force a downstream target with a valid cache entry to execute. Use [phases](./workspace/phase) for workspace-wide barriers and direct dependencies for concrete producer-consumer relationships.

## Compatible targets were not batched

Common reasons include a single compatible required node, no member that needs execution, a non-batchable command, different cluster identities, `batch = ~never`, different phases, or an external dependency cycle that prevents safe contraction.

The [batch guide](./getting-started/batch#why-no-batch-was-created) explains each case. Remember that `batch = ~never` creates individual commands but does not make them sequential.

## Target is waiting for a named lock

Another thread, batch, or Terrabuild process currently owns the same machine-global lock name. Waiting is expected when those operations mutate the same shared resource. The lease is released automatically after normal completion, exceptions, or process termination.

Use `explain` to check the inherited lock names. A debug run records lock-wait time separately from execution time. If unrelated work shares a name, give the resources distinct lock names or override an inherited workspace lock in the project. Do not remove the lock merely to hide contention when the commands can still collide.

`terrabuild clear --all` removes idle target and cache-restore lock files but leaves locks owned by active processes intact. If a process dies while replacing cached outputs, the next restore rolls the journaled transaction back before trying again.

## Environment-sensitive input was rejected

Targets are environment-neutral by default. Terrabuild rejects a neutral target that directly or transitively consumes sensitive predefined inputs such as environment, branch, tag, or CI state.

Run `explain` with the same options to see each target and input. Remove accidental contextual dependencies from portable builds. Set `environment_sensitive = true` only when the value intentionally changes the result, such as a staging-specific deployment plan; the consumed sensitive value hashes then participate in the cache key.

## Troubleshoot with Codex or Claude Code

Terrabuild provides a portable [Terrabuild skill](https://github.com/MagnusOpera/Terrabuild/blob/main/docs/guides/SKILL.md) for AI coding agents. It helps an agent choose between `terrabuild explain` and a debug run, investigate cache and rebuild decisions, find failed operations, interpret partial diagnostics, and identify performance bottlenecks without treating an uncached build as a normal measurement.

The skill is guidance, not an executable plugin. Review it before installation and keep the downloaded copy updated when Terrabuild's diagnostic format or commands change.

### Install for Codex

Install the skill for the current repository:

```bash
mkdir -p .agents/skills/terrabuild
curl -fsSL https://raw.githubusercontent.com/MagnusOpera/Terrabuild/main/docs/guides/SKILL.md \
  -o .agents/skills/terrabuild/SKILL.md
```

Codex discovers repository skills from `.agents/skills`. To make the skill available in every repository instead, save it as `~/.agents/skills/terrabuild/SKILL.md`. Codex can select it automatically for matching requests, or you can invoke it explicitly with `$terrabuild`. Restart Codex if a newly created skill directory is not detected. See the [official Codex skills documentation](https://developers.openai.com/codex/skills) for discovery scopes and skill management.

### Install for Claude Code

Install the same skill for the current repository:

```bash
mkdir -p .claude/skills/terrabuild
curl -fsSL https://raw.githubusercontent.com/MagnusOpera/Terrabuild/main/docs/guides/SKILL.md \
  -o .claude/skills/terrabuild/SKILL.md
```

Claude Code discovers repository skills from `.claude/skills`. To make the skill available in every repository instead, save it as `~/.claude/skills/terrabuild/SKILL.md`. Claude can select it automatically for matching requests, or you can invoke it explicitly with `/terrabuild`. Restart Claude Code if a newly created top-level skill directory is not detected. See the [official Claude Code skills documentation](https://code.claude.com/docs/en/skills) for discovery scopes and skill management.

## Project does not rebuild after a file changes {#outside-files}

By default, Terrabuild tracks files below the project path. Add files from a parent directory or sibling project with the `includes` attribute on the [project](/docs/project) block.

## .NET props files are not detected

[Props files](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory?view=vs-2022) can sit above the project directory. The [.NET extension](/docs/extensions/dotnet) does not search parent directories for them.

Add those files to the project block's `includes` attribute so they participate in change detection.

## Container does not receive an environment variable

Terrabuild does not pass host environment variables into containers by default. List each required name in the extension's `variables` attribute in either the [workspace](/docs/workspace/extension) or [project](/docs/project/extension) block.

```
extension @dotnet {
    image = "mcr.microsoft.com/dotnet/sdk:8.0.302"
    variables = [
        "DOTNET_NOLOGO"
        "DOTNET_CLI_TELEMETRY_OPTOUT"
        "DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK"
    ]
}
```
