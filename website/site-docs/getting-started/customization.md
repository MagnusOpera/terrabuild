---
title: Customize your tools
description: Configure included extensions, use existing scripts, or add an FScript integration.
---

Terrabuild comes with integrations for common tools. You can start with their
defaults, adapt their settings, and introduce your own actions as the repository
grows. Choose the smallest amount of customization that expresses what you need.

| Need | Approach |
| --- | --- |
| Change an action's arguments | Set arguments on its invocation inside a target. |
| Share a tool version or default argument | Configure an `extension` in `WORKSPACE`. |
| Give one project a different container image | Specialize the extension in `PROJECT`. |
| Run an existing script or an occasional command | Use `@shell`. |
| Give a tool a reusable vocabulary or metadata discovery | Write an FScript extension under a custom name. |

## Configure an included extension

For example, centralize the .NET configuration:

```terrabuild title="WORKSPACE"
extension @dotnet {
  defaults {
    configuration = "Release"
  }
}
```

A project can then use `@dotnet build { }`. An explicit argument on the action,
such as `@dotnet build { configuration = "Debug" }`, takes precedence over the
default. Use `image` on the extension to run the tool in a container.

Project extension settings inherit workspace settings. Scalars such as `image`
can be replaced. Collections have different rules: `variables` is additive, and
`defaults` and `env` may add keys but cannot replace inherited keys. See
[Extension block](../project/extension.md) before sharing settings across projects.

## Give a script a reusable action

In the [first tutorial](./quick-start.md), both projects call `sh build.sh` through
`@shell`. Let us give that convention a name using **FScript**, the language used
for Terrabuild extensions.

Create a `tools` directory at the workspace root and add:

```fsharp title="tools/workflow.fss"
type ShellOperation =
  { Command: string
    Arguments: string
    ErrorLevel: int }

type CommandResult =
  { Batchable: bool
    Operations: ShellOperation list }

type ExportFlag =
  | Dispatch
  | Default
  | Never
  | Local
  | External
  | Remote

[<export>] let build (context: {| Directory: string |}) : CommandResult =
  { Batchable = false
    Operations =
      [ { Command = "sh"
          Arguments = "build.sh"
          ErrorLevel = 0 } ] }

{ [nameof build] = [Local] }
```

Read the script in three parts:

1. The records describe the operation returned to Terrabuild: a command,
   arguments, and the accepted exit-code threshold.
2. The exported `build` function returns one operation. Terrabuild supplies
   `context`; this action uses the fixed `build.sh` convention in each project.
3. The final descriptor maps `build` to `Local`, declaring local artifact caching
   as its default. `Batchable = false` keeps projects in separate invocations.

The extension returns instructions for execution. It does not run the build
while Terrabuild is discovering or explaining the graph.

## Register and call it

Add to `WORKSPACE`:

```terrabuild title="WORKSPACE"
extension workflow {
  script = "tools/workflow.fss"
}
```

Replace the `build` target in each tutorial `PROJECT` with:

```terrabuild
target build {
  workflow build { }
}
```

Keep the project blocks and the shared workspace target policy. Then run:

```bash
terrabuild explain build --project package
terrabuild run build --project package
cat package/dist/package.txt
```

The dependency order and output remain the same. Changing the implementation
changes the task identity, so the first run with this extension may execute
again. Later runs can reuse the result.

You now have a repository-owned integration. Its action could later return a
native tool command instead of `sh`, accept typed arguments, or discover outputs
through an optional defaults handler.

## Replace an integration deliberately

Built-in identifiers such as `@dotnet` and `@terraform` reserve their script
implementations. You can configure them, but assigning them a different `script`
is rejected. To change the implementation, register an extension under a custom
name, then use that name in the relevant targets and project initializers.

This lets a repository use the included extension in some projects and a custom
integration in others. Custom script paths are relative to the configuration
file that declares them and must stay inside the workspace. HTTPS scripts are
also supported.

## Expand only when needed

The [FScript extension guide](../extensibility/script.md) explains typed action
arguments, dispatch, project defaults, artifact flags, and batching. Use the
[protocol types](../extensibility/types.md) and
[host functions](../extensibility/functions.md) when you need their exact shapes.

For arbitrary shell commands, choose cache policies deliberately. A command
that deploys, publishes mutable state, or observes a live service may need
`build = ~always` and `artifacts = ~none`. Returning an operation does not itself
make that operation deterministic.
