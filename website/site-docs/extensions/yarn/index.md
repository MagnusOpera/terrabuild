---
title: "yarn"
---



## Available Commands
| Command | Description |
|---------|-------------|
| [dispatch](/docs/extensions/yarn/dispatch) | Runs an arbitrary yarn command (Terrabuild action name is forwarded to `yarn`). |
| [install](/docs/extensions/yarn/install) | Installs packages with `yarn install`, optionally updating the lockfile or ignoring engines. |
| [build](/docs/extensions/yarn/build) | Runs the `build` script via `yarn build`. |
| [test](/docs/extensions/yarn/test) | Runs the `test` script via `yarn test`. |
| [run](/docs/extensions/yarn/run) | Runs an arbitrary yarn script (`yarn <command>`). |

## Project Initializer
Computes default outputs and dependencies from `package.json`.
```
project {
  @yarn { }
}
```
Equivalent to:
```
project {
    ignores = [ "node_modules/**" ]
    outputs = [ "dist/**" ]
}
```
