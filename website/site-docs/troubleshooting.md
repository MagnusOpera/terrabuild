---
title: Troubleshooting

---

Terrabuild has limitations like all tools. This document explains common errors, misunderstandings, and how to resolve them.

## My project does not recompile despite a file has changed {#outside-files}

If your project references files outside the project hierarchy, use the `includes` attribute on the [project](/docs/project) block. By default, Terrabuild only tracks files below the project path. If you need to track files from parent directories or sibling projects, explicitly include them using glob patterns.

## Support of props files in .net

[Props files](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory?view=vs-2022) are not automatically supported as they lie outside the project structure and are not explicitly referenced. The [Dotnet](/docs/extensions/dotnet) extension does not attempt to find such files automatically.

If you still need to track dependencies on such files, use the `includes` attribute on the [project](/docs/project) block to explicitly include the props files in change detection. 

## Build fails to use environment variables

If your build fails to use environment variables, you are likely using Docker containers. By default, environment variables from the host are not passed to containers. To allow specific environment variables to be passed to the container, use the `variables` parameter when configuring the extension in either the [workspace](/docs/workspace/extension) or [project](/docs/project/extension) block.

```
extension @dotnet {
    image = "mcr.microsoft.com/dotnet/sdk:8.0.302"
    variables = [
        "DOTNET_NOLOGO"
        "DOTNET_CLI_TELEMETRY_OPTOUT"
        "DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK"
    ]
}
```
