---
title: Variable Block

---

The `variable` block defines a variable that can be provided via the command line or environment variables. Variables allow you to parameterize your build configuration and make it flexible for different environments, configurations, or build scenarios.

## Example Usage
```
# define config variable - the default value is used if not provided via --variable
variable config {
  description = "configuration to build"
  default = "Debug"
}
```

## Argument Reference

The following arguments are supported:

* `description` - (Optional) Description of the variable. This helps document what the variable is used for and appears in help text.
* `default` - (Optional) Default value for the variable if not provided via command line or environment. It must evaluate to a scalar value (string, number, boolean) if provided.

## Variable Override

Variables **must** be declared in the WORKSPACE file before they can be used.

Terrabuild supports variable overriding using the following precedence order (first match wins):
1. **Environment variable**: Use `TB_VAR_xxx` to override a variable (where `xxx` is the variable name). For example, `TB_VAR_config=Release` overrides the `config` variable. Note variable name is case insensitive.
2. **Command line argument**: Use the `--variable` or `-v` switch when building a target. For example, `terrabuild run build --variable config=Release`.
3. **Default value**: If neither of the above is provided, the default value from the variable declaration is used.
