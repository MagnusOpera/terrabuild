---
title: "test"
---
Runs tests with `cargo test`.
```
@cargo test {
    profile = "dev"
    args = "--blame-hang"
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
* `args` - (Optional) Extra `cargo test` arguments.
