---
title: Functions

---

Functions can be applied on values. Functions take a tuple as argument `(param1, param2)` which can be built from aforementioned primitives.

## Boolean

| Function | Description | Usage | Result |
|----------|-------------|-------|--------|
| \|\| | or operation. | `true \|\| false` | `true` |
| && | and operation. | `false && true` | `false` |

## String

| Function | Description | Usage | Result |
|----------|-------------|-------|--------|
| + | concatenate two strings | `"Hello" + "world"` | `"Hello world"` |
| trim | remove leading and trailing spaces | `trim("  Hello world  ")` | `"Hello world"` |
| upper | convert string to upper case | `upper("Hello world")` | `"HELLO WORLD"` |
| lower | convert string to lower case | `lower("Hello WORLD")` | `"hello world"` |
| replace | replace in string. | `replace("Hello world", "world", "Terrabuild")` | `Hello Terrabuild` |
| regex match | match string against a regex. | `"prodfr" ~= "^prod"` | `true` | 

## Number

| Function | Description | Usage | Result |
|----------|-------------|-------|--------|
| + | add two numbers | `5 + 2` | `7` |
| - | substract two numbers | `5 - 2` | `3` |
| * | multiply two numbers | `5 * 2` | `10` |
| / | divide two numbers | `6 / 2` | `3` |

## List

| Function | Description | Usage | Result |
|----------|-------------|-------|--------|
| Static Item | Get item at position: error if index is not valid | `[1, 2, 3].1` | `2` |
| Dynamic Item | Get item using expression: error if index is not valid | `[1, 2, 3].[1]` | `2` |
| Count | Get number of element of list | `count([1, 2, 3])` | `3` |
| + | Concatenate two lists | `[1, 2, 3] + [4, 5, 6]` | `[1, 2, 3, 4, 5, 6]`

## Map

| Function | Description | Usage | Result |
|----------|-------------|-------|--------|
| Static Item | Get named item (using identifier): error if index is not valid | `{ a: 1, b: 2 }.b` | `2` |
| Dynamic Item | Get item using expression: error if index is not valid | `{ a: 1, b: 2 }.["b"]` | `2` |
| Count | Get number of element of map | `count({ a: 1, b: 2 })` | `2` |
| + | Merge two maps | `{ a: 1, b: 1} + { b: 2, c: 2}` | `{ a: 1, b: 2, c: 2}` |

## Generic

| Function | Description | Usage | Result |
|----------|-------------|-------|--------|
| Not | `true` if falsy (`nothing` or `false`) | `!false` | `true` |
| Equal | compares two values for equality | `"env" = "prod"` | `false` |
| Not Equal | compares two values for inequality | `"env" != "prod"` | `true` |
| Null-Coalesce | return value or alternate value if falsy (`nothing` or `false`) | `nothing ?? 42` | `42` |
| Ternary Conditional | checks boolean value and returns truthy or falsy value | `nothing ? 42 : 666` | `666` |
