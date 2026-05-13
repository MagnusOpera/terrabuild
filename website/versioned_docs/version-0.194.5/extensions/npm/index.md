---
title: "npm"
---



## Available Commands
| Command | Description |
|---------|-------------|
| [dispatch](/docs/extensions/npm/dispatch) | Runs an arbitrary npm command (forwards the Terrabuild action name to `npm`). |
| [install](/docs/extensions/npm/install) | Installs packages with `npm ci`, honoring the lock file. |
| [build](/docs/extensions/npm/build) | Runs the `build` script via `npm run build`. |
| [test](/docs/extensions/npm/test) | Runs the `test` script via `npm run test`. |
| [run](/docs/extensions/npm/run) | Runs an arbitrary npm script via `npm run <target>`. |
| [exec](/docs/extensions/npm/exec) | Executes a package binary via `npm exec`. |

## Project Initializer
Computes default outputs and dependencies from `package.json`.
```
project {
  @npm { }
}
```
Equivalent to:
```
project {
    ignores = [ "node_modules/**" ]
    outputs = [ "dist/**" ]
}
```
