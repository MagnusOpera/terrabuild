---
title: "dotnet"
---



## Available Commands
| Command | Description |
|---------|-------------|
| [dispatch](/docs/extensions/dotnet/dispatch) | Runs an arbitrary `dotnet` command (action name is forwarded to `dotnet`). |
| [tool](/docs/extensions/dotnet/tool) | Executes `dotnet tool ...` commands. |
| [restore](/docs/extensions/dotnet/restore) | Restores NuGet packages for one project or a batch solution. |
| [build](/docs/extensions/dotnet/build) | Build project. |
| [pack](/docs/extensions/dotnet/pack) | Packs the project into a NuGet package via `dotnet pack`. |
| [publish](/docs/extensions/dotnet/publish) | Publishes binaries via `dotnet publish` for one project or a batch solution. |
| [test](/docs/extensions/dotnet/test) | Runs tests via `dotnet test`, optionally batched through a generated solution. |

## Project Initializer
Computes default outputs and project-reference dependencies.
```
project {
  @dotnet { }
}
```
Equivalent to:
```
project {
    ignores = [ "**/*.binlog" ]
    outputs = [ "bin/" "obj/" "**/*.binlog" ]
}
```
