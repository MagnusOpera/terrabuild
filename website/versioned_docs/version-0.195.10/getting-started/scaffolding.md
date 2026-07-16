---
title: Scaffolding

prev: /docs/getting-started/quick-start

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

# Building
Now, it's time to explore [WORKSPACE](/docs/workspace) and [PROJECT](/docs/project) configurations, since this probably won't work out of the box 😅 - but most of the work is done!

You can try building your workspace using:
```
cd <path-to-repository>
terrabuild run build
```

Since this probably won't work out of the box 😅. You are invited to explore [WORKSPACE](/docs/workspace) and [PROJECT](/docs/project) configurations.

Also, check the [troubleshooting](/docs/troubleshooting) page, this can help you getting started.

If everything is ok, congrats ! Keep exploring configuration as your workspace can benefit from further tuning.

- [workspace](/docs/workspace)
- [project](/docs/project)