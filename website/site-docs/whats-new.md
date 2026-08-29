---
id: whats-new
title: What's New
slug: /whats-new
---

For the complete history, see the full [CHANGELOG.md](https://github.com/MagnusOpera/Terrabuild/blob/main/CHANGELOG.md) on GitHub.

## 0.199.1

### 0.199.1

- Updated all Terrabuild website documentation to link directly to the FScript custom domain without legacy URL rewriting.

### 0.199.0

- Select projects by identifier or workspace-relative path and reject unknown `--project` values.
- Remove stale declared outputs when restoring a cached target.
- Avoid executing batches that contain only cached targets.
- Prevent batch contraction from introducing execution cycles.
- Publish the website from GitHub Actions to Cloudflare Workers Static Assets.
- Rewrite the website landing page to explain how Terrabuild handles build and deployment on the same dependency graph.
- Rewrite the website documentation with direct build and deployment guidance, a dedicated deployment guide, and qualified cache behavior.

**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/0.198.4...0.199.1
