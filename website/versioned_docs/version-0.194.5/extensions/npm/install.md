---
title: "install"
---
Installs packages with `npm ci`, honoring the lock file.
```
@npm install {
    force = false
    clean = true
    args = "--install-strategy hoisted"
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | local
| Bach      | no

## Argument Reference
The following arguments are supported:
* `force` - (Optional) Adds `--force`. Default value is `false`.
* `clean` - (Optional) Uses `clean-install` when true. Default value is `true`.
* `args` - (Optional) Extra `npm ci` arguments.
