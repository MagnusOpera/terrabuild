---
title: "execute"
---
Executes a `.fss` file through the `fscript` CLI with the current project as sandbox root.
```
@fscript execute {
    script = "scripts/write-version.fss"
    args = [ "arg1" "arg 2" ]
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | never
| Bach      | no

## Argument Reference
The following arguments are supported:
* `script` - (Required) Path to the FScript file, relative to the current project unless absolute.
* `args` - (Optional) Arguments forwarded after `--`.
