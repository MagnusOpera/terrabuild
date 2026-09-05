---
title: Adopt your existing tools
description: Add Terrabuild to a repository progressively, starting with a command you already trust.
---

Start with one command that already works in your repository. Terrabuild will
coordinate it with other work; your native project files remain the source of
truth for compilation, dependencies, and tool-specific options.

You can [scaffold recognized projects](./scaffolding.md) or write the first
`WORKSPACE` and `PROJECT` yourself. In either case, begin with one useful target
and check its outputs before extending the graph.

## 1. Wrap a familiar build

For an existing .NET project, put this `PROJECT` beside its project file:

```terrabuild title="src/api/PROJECT"
project api {
  @dotnet { }
}

target build {
  @dotnet build { configuration = "Release" }
}
```

At the repository root:

```terrabuild title="WORKSPACE"
workspace { }

target build {
  depends_on = [ target.^build ]
}
```

The two uses of `@dotnet` have different roles. Inside `project`, its initializer
reads native project metadata, including dependencies and outputs. Inside
`target build`, its action describes the command to execute.

The .NET SDK must be available on the host, or you can
[configure a container image](../extensibility/container.md) for the extension.
An included extension supplies the integration; it does not install the native
tool on your machine.

From the repository root:

```bash
terrabuild explain build --project api
terrabuild run build --project api
```

Check the compiled files, then repeat the command to check reuse. If the native
project references another project, add a `PROJECT` there too so Terrabuild can
represent the dependency. Give it a `build` target with the appropriate action.

## 2. Add another toolchain

A frontend with existing `install` and `build` requirements can use npm:

```terrabuild title="src/web/PROJECT"
project web {
  outputs = [ "dist/**" ]
  @npm { }
}

target build {
  @npm install { }
  @npm build { }
}
```

This assumes `package.json` has a `build` script that writes to `dist/`. Adjust
the outputs to match your application. Commands inside one target run in their
declared order; independent project targets can run concurrently.

```bash
terrabuild run build
```

Now the shared target name coordinates two different tools. It does not require
them to share a package manager or a build language.

## 3. Describe dependencies the tools cannot discover

Native project references cover some relationships. Others cross tool boundaries:
for example, a web application may consume a client generated from an API schema.
Add a project dependency for that relationship, then name the required target.

```terrabuild
project web {
  depends_on = [ project.client ]
  outputs = [ "dist/**" ]
  @npm { }
}

target build {
  depends_on = [ target.^generate ]
  @npm install { }
  @npm build { }
}
```

Here, the `client` project must expose a `generate` target. The workspace's
`target.^build` rule and this project's `target.^generate` rule are combined.
See [generated code](./advanced-scenarios.md#generated-code-before-consumers) for
the complete relationship.

## 4. Check what may be reused

Before scaling up, verify three things:

| Check | Why it matters |
| --- | --- |
| Source files and tool configuration are tracked inputs. | A change must invalidate the affected result. |
| Output patterns match generated artifacts. | Terrabuild must know which files to save and restore. |
| Commands have appropriate artifact and build policies. | A publish or deployment action may need different treatment from compilation. |

Try a second run, check restoration through a consuming target that executes,
then change a source file. These are the same experiments as the [first tutorial](./quick-start.md).
They establish that the model works before it becomes a dependency for more work.

Included extensions supply defaults, but they cannot know every custom output
path or external input. Use [target policies](./target-policies.md) to make those
choices explicit.

## 5. Move the same request into CI

After checkout and tool installation, CI can invoke:

```bash
terrabuild run build
```

Keep repository relationships in `WORKSPACE` and `PROJECT`. Keep event triggers,
runner provisioning, credentials, and approvals in CI. Add
[Insights](./insights.md) when you want artifact reuse across machines and an
execution history.

Next, [configure environments](./environments.md) or
[customize the included integrations](./customization.md).
