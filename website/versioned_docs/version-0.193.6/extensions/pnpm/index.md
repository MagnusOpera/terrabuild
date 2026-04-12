---
title: "pnpm"
---



## Available Commands
| Command | Description |
|---------|-------------|
| [dispatch](/docs/extensions/pnpm/dispatch) | Runs an arbitrary pnpm command (Terrabuild action name is forwarded to `pnpm`). |
| [install](/docs/extensions/pnpm/install) | Installs packages with `pnpm install`, optionally honoring the lockfile and batching across workspaces. |
| [build](/docs/extensions/pnpm/build) | Runs the `build` script (`pnpm run build`) across targeted workspaces. |
| [test](/docs/extensions/pnpm/test) | Runs the `test` script (`pnpm run test`) across targeted workspaces. |
| [run](/docs/extensions/pnpm/run) | Runs an arbitrary pnpm script (`pnpm run <target>`). |

## Project Initializer
Computes default outputs and dependencies from `package.json`.
```
project {
  @pnpm { }
}
```
Equivalent to:
```
project {
    ignores = [ "node_modules/**" ]
    outputs = [ "dist/**" ]
}
```
