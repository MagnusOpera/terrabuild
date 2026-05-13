---
title: "cargo"
---



## Available Commands
| Command | Description |
|---------|-------------|
| [dispatch](/docs/extensions/cargo/dispatch) | Runs an arbitrary Cargo subcommand (Terrabuild action name is forwarded to `cargo`). |
| [build](/docs/extensions/cargo/build) | Build project. |
| [test](/docs/extensions/cargo/test) | Runs tests with `cargo test`. |

## Project Initializer
Computes default outputs and dependencies from `Cargo.toml`.
```
project {
  @cargo { }
}
```
Equivalent to:
```
project {
    ignores = [ ]
    outputs = [ "target/debug/" "target/release/" ]
}
```
