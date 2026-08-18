---
id: whats-new
title: What's New
slug: /whats-new
---

For the complete history, see the full [CHANGELOG.md](https://github.com/MagnusOpera/Terrabuild/blob/main/CHANGELOG.md) on GitHub.

## 0.198.4

### 0.198.4

- Upgrade the embedded FScript language and runtime packages to `0.78.1`.
- Load different FScript extensions concurrently while continuing to prepare each script only once.
- Build configurations faster by scanning projects and evaluating their dependency graph concurrently.
- Speed up graph refinement while preserving graph contents, target hashes, and execution decisions.
- Improve the Terrabuild skill for build diagnosis and performance investigation, and document its installation in Codex and Claude Code.
- Realize lazy dependencies when restored targets are promoted into an executing batch.
- Hide ignored nodes from the local console execution graph by default and allow showing them from Advanced controls.
- Summarize selected environment-sensitivity violations at the top of `explain` output.
- Finalize failed preparation diagnostics with the latest partial graph and error, and direct environment-sensitivity failures to `explain`.
- Enforce environment-neutral targets by default and hash sensitive values only after explicit opt-in.
- Add inherited `environment_sensitive` target opt-in and migration diagnostics.
- Warn and report when selected targets consume environment-sensitive built-in inputs.
- Replace the `run --what-if` option with the breaking `run --dry-run` spelling.
- Show diagnostic action, cache, input, and operation explanations in the local console.
- Add a readable `explain` command backed by the canonical diagnostic report.
- Restore secret-safe resolved operation and evaluated input details in diagnostic JSON reports.
- Avoid realizing build-time dependencies behind restored or summarized nodes while preserving direct lazy prerequisites of executing targets.
- Keep the rolling documentation labelled `Next` while selecting the latest stable Terrabuild tag as the default released documentation version.
- Add a Getting Started guide for connecting Terrabuild to Insights, sharing encrypted artifacts, and reporting builds from developer machines and CI.

**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/0.197.9...0.198.4
