---
id: whats-new
title: What's New
slug: /whats-new
---

For the complete history, see the full [CHANGELOG.md](https://github.com/MagnusOpera/Terrabuild/blob/main/CHANGELOG.md) on GitHub.

## 0.204.0

### 0.204.0

- Validate interpreted extensions and self-builds against the published FScript 0.84.0 packages.
- Use the independent FScript extension SDK for protocol types and provider registration.
- Expose extension protocol types through FFI and provide an FScript editor configuration for extension authors.
- Target FScript 0.84.0 while retaining AST interpretation, immutable protocol metadata, and isolated extension exit status.
- Adopt FScript contextual `export let` declarations in extensions, fixtures, and documentation generation.
- Declare the metadata context accepted by built-in extension defaults to match the values supplied during project discovery.
- Convert extension arguments to their declared FScript record, option, and union types for compatibility with signature validation.
- Require explicit preserved, broken, or unaffected provenance outcomes from every extension command.
- Carry extension-returned provenance declarations and original production references to Insights across cache reuse.

**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/0.203.0...0.204.0
