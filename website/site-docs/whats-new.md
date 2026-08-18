---
id: whats-new
title: What's New
slug: /whats-new
---

For the complete history, see the full [CHANGELOG.md](https://github.com/MagnusOpera/Terrabuild/blob/main/CHANGELOG.md) on GitHub.

## 0.198.3-next

### 0.198.3-next

- Realize lazy dependencies when restored targets are promoted into an executing batch.

### 0.198.2-next

- Hide ignored nodes from the local console execution graph by default and allow showing them from Advanced controls.

### 0.198.1-next

- Summarize selected environment-sensitivity violations at the top of `explain` output.
- Finalize failed preparation diagnostics with the latest partial graph and error, and direct environment-sensitivity failures to `explain`.
- Enforce environment-neutral targets by default and hash sensitive values only after explicit opt-in.
- Add inherited `environment_sensitive` target opt-in and migration diagnostics.
- Warn and report when selected targets consume environment-sensitive built-in inputs.
- Replace the `run --what-if` option with the breaking `run --dry-run` spelling.
- Show diagnostic action, cache, input, and operation explanations in the local console.
- Add a readable `explain` command backed by the canonical diagnostic report.
- Restore secret-safe resolved operation and evaluated input details in diagnostic JSON reports.

### 0.198.0-next

- Avoid realizing build-time dependencies behind restored or summarized nodes while preserving direct lazy prerequisites of executing targets.
- Keep the rolling documentation labelled `Next` while selecting the latest stable Terrabuild tag as the default released documentation version.
- Add a Getting Started guide for connecting Terrabuild to Insights, sharing encrypted artifacts, and reporting builds from developer machines and CI.

**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/0.197.9-next...0.198.3-next
