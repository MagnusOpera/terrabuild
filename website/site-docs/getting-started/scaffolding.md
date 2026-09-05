---
title: Scaffold a monorepo
description: Generate an initial Terrabuild model for an existing repository.
---

The scaffold command discovers common project types and writes a starting
Terrabuild configuration:

```bash
terrabuild scaffold --workspace <path-to-repository>
```

It recognizes .NET project files, npm packages, Makefiles, Dockerfiles, and
Terraform projects.

By default, scaffolding does not replace an existing `WORKSPACE` or `PROJECT`
file. Add `--force` only when you intend to regenerate those files.

## What it creates

After discovery, the repository contains:

- one `WORKSPACE` file at the monorepo root for shared targets and extension defaults;
- one `PROJECT` file in each detected buildable or deployable unit.

Typical output looks like this:

```text
 ✔ PROJECT src/apps/api
 ✔ PROJECT src/apps/web
 ✔ PROJECT src/libs/shared
 ✔ PROJECT src/deploy
 ✔ WORKSPACE
```

## Review the model before running it

Scaffolding establishes a useful baseline; it cannot infer every repository
rule. Review:

1. **Project boundaries.** Confirm each `PROJECT` represents a meaningful unit.
2. **Project dependencies.** Add relationships that are not visible from native project files.
3. **Outputs.** Confirm generated files and directories are described correctly.
4. **Targets.** Remove irrelevant generated targets and add repository-specific commands.
5. **Deployment.** Treat plans, applies, and cleanup as deliberate environment-sensitive or side-effecting work.

Discovery stops descending below a recognized project root. If an expected
project is absent, check whether a parent Makefile, Dockerfile, package, or
project file caused that directory to become a boundary.

## Start with one outcome

Run or inspect a familiar target before modeling the entire delivery lifecycle:

```bash
cd <path-to-repository>
terrabuild explain build
terrabuild run build
```

Once builds and project dependencies are correct, add distribution and
deployment paths. See [Adopt your existing tools](./existing-repository.md) for a progressive walkthrough and
[Model a deployment](./deployment.md) for the source-to-environment path.

Use the [Workspace reference](../workspace/index.md) and
[Project reference](../project/index.md) when you need the complete file syntax.
