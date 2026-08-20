---
title: Troubleshooting

---

This page covers configuration mistakes and inputs Terrabuild cannot discover without help.

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
