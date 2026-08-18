---
title: Troubleshooting

---

Terrabuild has limitations like all tools. This document explains common errors, misunderstandings, and how to resolve them.

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

## My project does not recompile despite a file has changed {#outside-files}

If your project references files outside the project hierarchy, use the `includes` attribute on the [project](/docs/project) block. By default, Terrabuild only tracks files below the project path. If you need to track files from parent directories or sibling projects, explicitly include them using glob patterns.

## Support of props files in .net

[Props files](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory?view=vs-2022) are not automatically supported as they lie outside the project structure and are not explicitly referenced. The [Dotnet](/docs/extensions/dotnet) extension does not attempt to find such files automatically.

If you still need to track dependencies on such files, use the `includes` attribute on the [project](/docs/project) block to explicitly include the props files in change detection. 

## Build fails to use environment variables

If your build fails to use environment variables, you are likely using Docker containers. By default, environment variables from the host are not passed to containers. To allow specific environment variables to be passed to the container, use the `variables` parameter when configuring the extension in either the [workspace](/docs/workspace/extension) or [project](/docs/project/extension) block.

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
