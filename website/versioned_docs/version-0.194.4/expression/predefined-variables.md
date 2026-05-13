---
title: Predefined Variables

---
Terrabuild provides several predefined variables that can be used in expressions. These variables provide information about the build context, system environment, and current project/target being built.

| Name | Description | Scope |
|----------|---------|-------|
| `terrabuild.configuration` | Name of current configuration | Global |
| `terrabuild.environment` | Name of current environment | Global |
| `terrabuild.branch_or_tag` | Name of current branch or tag | Global |
| `terrabuild.head_commit` | Head commit hash | Global |
| `terrabuild.retry` | `true` if build is retried. | Global |
| `terrabuild.force` | `true` if build is forced. | Global |
| `terrabuild.ci` | `true` if build is running in known CI. | Global |
| `terrabuild.engine` | Container engine used. | Global |
| `terrabuild.debug` | `true` if debug is enabled. | Global |
| `terrabuild.tag` | Tag provided by user or `nothing`. | Global |
| `terrabuild.note` | Note provided by user or `nothing`. | Global |
| `terrabuild.os` | `darwin`, `windows`, `linux` or `nothing` if unknown. | Global |
| `terrabuild.arch` | `arm64`, `amd64` or `nothing` if unknown. | Global |
| `terrabuild.project_slug` | Slug of current [project](/docs/project) (relative path from workspace) | Target |
| `terrabuild.project` | Id of current [project](/docs/project) if defined | Target |
| `terrabuild.target` | Name of current [target](/docs/project/target) | Target |
| `terrabuild.version` | Version (hash) of current [project](/docs/project) | Target |
| `project.<id>.version` | Version (hash) of given project `<id>` | Target |
