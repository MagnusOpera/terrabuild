---
id: whats-new
title: What's New
slug: /whats-new
---

For the complete history, see the full [CHANGELOG.md](https://github.com/MagnusOpera/Terrabuild/blob/main/CHANGELOG.md) on GitHub.

## 0.197.0-next

- Connect CI self-builds to the Insights staging cache with an explicit login/logout lifecycle, and fix logout so it removes only the selected workspace credentials.
- Build repository-scoped, versioned .NET SDK (`@dotnetsdk`) and pnpm toolchain images in a dedicated prerequisite phase, authenticate CI image publication to GHCR, use those images consistently for local and CI self-builds, and upgrade the .NET SDK to `10.0.302`.
- Add dedicated Console documentation with Terrabuild source examples and live screenshots explaining controls, project-node shapes, cache-status colors, dependency arrows, phases, and graph navigation.

**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/0.196.4-next...0.197.0-next
