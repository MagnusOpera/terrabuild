---
title: Identifier

---

An identifier starts with an ASCII letter and may continue with letters or numbers. A single underscore can separate groups. Lowercase `snake_case` is the recommended convention, but uppercase letters are accepted.

**Underscore rules:**
* Underscores (`_`) can be used within the identifier
* Only a single consecutive `_` is allowed (no `__`)
* Underscores cannot be at the start or end of the identifier
* An underscore must always be followed by a character (not at the end)

You will encounter identifiers when defining targets, phases, extensions, variables, and locals.

There are some syntax extensions for specific usages:
* Target reference identifier can start with `^` (for example `^build`) - see [target configuration](/docs/workspace/target).
* Internal extension identifier starts with `@` (for example `@dotnet`) - see [project configuration](/docs/project).

## Examples

### ✅ Valid identifiers
* `config` - Simple identifier
* `project42` - Contains numbers
* `this_is_a_var` - Single underscore between words
* `my_var_123` - Underscore followed by numbers
* `a` - Single letter
* `version_1_0` - Multiple single underscores
* `Config` - Uppercase letters are accepted, although lowercase is recommended

### ❌ Invalid identifiers
* `_config` - Cannot start with underscore
* `config_` - Cannot end with underscore
* `project__42` - Two consecutive underscores are not allowed
* `this_is_a_var_` - Trailing underscore (underscore must be followed by a character)
* `_` - Cannot be just an underscore
* `var%` - Invalid character `%`
* `my-var` - Hyphens are not allowed (use underscore instead)
* `my.var` - Dots are not allowed (use underscore instead)
* `123project` - Cannot start with a number
