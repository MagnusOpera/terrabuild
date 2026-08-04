---
id: whats-new
title: What's New
slug: /whats-new
---

For the complete history, see the full [CHANGELOG.md](https://github.com/MagnusOpera/Terrabuild/blob/main/CHANGELOG.md) on GitHub.

## 0.197.3-next

### 0.197.3-next

- Show extension dispatch handlers as the user-supplied `<command>` placeholder in generated documentation.

### 0.197.2-next

- Preserve cached summaries for restored members when another member of the same batch must execute.
- Let extensions persist successful command stdout to project files, including a dedicated Terraform output action.
- Aggregate rolling Next and stable What's New notes across their release families while keeping each revision and Unreleased under its own heading.
- Publish website deployments from independently versioned `website-*.*.*` tags without triggering application releases.

### 0.197.1-next

- Require every commit targeting `main` to include a concise, user-facing `Unreleased` changelog entry.
- Upgrade the Terrabuild UI dependency set and refresh vulnerable transitive packages to address the current Vite, esbuild, and YAML security advisories.
- Patch the website dependency chain to remove current browser, build, and development-server security vulnerabilities.
- Define consistent `feat:`, `fix:`, and `chore:` commit-title conventions for repository changes.

### 0.197.0-next

- Connect CI self-builds to the Insights staging cache with an explicit login/logout lifecycle, and fix logout so it removes only the selected workspace credentials.
- Build repository-scoped, versioned .NET SDK (`@dotnetsdk`) and pnpm toolchain images in a dedicated prerequisite phase, authenticate CI image publication to GHCR, use those images consistently for local and CI self-builds, and upgrade the .NET SDK to `10.0.302`.
- Add dedicated Console documentation with Terrabuild source examples and live screenshots explaining controls, project-node shapes, cache-status colors, dependency arrows, phases, and graph navigation.

**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/0.196.4-next...0.197.3-next
