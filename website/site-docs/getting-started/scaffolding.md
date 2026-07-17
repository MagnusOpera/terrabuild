---
title: Scaffolding

prev: /docs/getting-started/key-concepts

---

Configuring a monorepo can be quite intimidating and tedious at first. This is why Terrabuild ships with the **scaffold** command to get you started.

Terrabuild can generate WORKSPACE and PROJECT files for the following languages:
* msbuild proj (.net, F#, C#, dacpac, ...)
* npm
* make
* docker
* terraform

```bash
terrabuild scaffold --workspace <path-to-repository> [--force]
```

By default, the `scaffold` command does not overwrite files. If you need to generate them again, use the `--force` flag.

:::info
  `scaffold` command does not explore a directory deeper if a known project type is found.
  If you miss some projects, check you have Makefile or Dockerfile files in the way.
:::

Upon completion, you will have several new files in your repository:
* WORKSPACE file at the root: this file defines the global configuration
* several PROJECT files in detected projects: this file defines the project configuration

Here is a sample output:
```
 ✔ PROJECT tests/scaffold/projects/dotnet-app
 ✔ PROJECT tests/scaffold/projects/make-app
 ✔ PROJECT tests/scaffold/projects/npm-app
 ✔ PROJECT tests/scaffold/libraries/dotnet-lib
 ✔ PROJECT tests/scaffold/deployments/terraform-deploy
 ✔ WORKSPACE
```

## Build the generated workspace

Scaffolding detects common project types and gives you a starting point. Review the generated [WORKSPACE](/docs/workspace) and [PROJECT](/docs/project) files before building because repository-specific targets, outputs, or extension settings may still need adjustment.

You can try building your workspace using:
```
cd <path-to-repository>
terrabuild run build
```

If the first build fails, use the generated configuration and the [troubleshooting guide](/docs/troubleshooting) to identify the missing project-specific settings.

- [workspace](/docs/workspace)
- [project](/docs/project)
