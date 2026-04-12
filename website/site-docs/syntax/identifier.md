---
title: Identifier

---

An identifier literal conforms to snake_case. It always starts with a character from `a-z` and is followed by one or more characters from `a-z` and `0-9`. 

**Underscore rules:**
* Underscores (`_`) can be used within the identifier
* Only a single consecutive `_` is allowed (no `__`)
* Underscores cannot be at the start or end of the identifier
* An underscore must always be followed by a character (not at the end)

You will encounter such identifiers when defining targets, environments, extensions, variables, and locals.

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
* `` `version` `` - Backtick-quoted identifier (allows special cases)

### ❌ Invalid identifiers
* `Config` - Uppercase letters are not allowed
* `_config` - Cannot start with underscore
* `config_` - Cannot end with underscore
* `project__42` - Two consecutive underscores are not allowed
* `this_is_a_var_` - Trailing underscore (underscore must be followed by a character)
* `_` - Cannot be just an underscore
* `var%` - Invalid character `%`
* `my-var` - Hyphens are not allowed (use underscore instead)
* `my.var` - Dots are not allowed (use underscore instead)
* `123project` - Cannot start with a number
