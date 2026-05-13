---
title: "build"
---
Builds the project with `cargo build`.
```
@cargo build {
    profile = "dev"
    args = "--keep-going"
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | remote
| Bach      | no

## Argument Reference
The following arguments are supported:
* `profile` - (Optional) Cargo profile name (lowercase, for example `release`). Default value is `dev`.
* `args` - (Optional) Extra `cargo build` arguments.
