---
title: Types and Literals

---

## Nothing

Value for no value (pun intended). Similar to `void` or `None` in other languages.

Literal is `nothing`.

## String

A string is a sequence of characters. A string starts with `"` and ends with `"`:
* `"this is a string"`
* `""`

Following characters must be escaped (double the character): `"`:
* `"Hello ""!"" "` is string `Hello "!"`

Strings support interpolation too ! Use `${ <expr> }` syntax:
* `"Hello ${ local.name } !"`

Note gollowing characters must be escaped (double the character): `{`, `}`, `"`:
* `"{{ Hello ""!"" }}"` is string `{ Hello "!" }`

## Boolean

Either literal `true` or `false`.

## Number

A number is a 32 bits signed integer:
* `42`
* `-123456`

## Enum

An enum is a specific identifier used in few places (see `cache` and `build`). For example:
* `~workspace`
* `~partition`

## List

A list is an ordered sequence of values. Values can be of different types, and lists can be nested.

Commas are optional. You can separate list items with whitespace or commas. For single-line lists, commas improve readability.

### Simple Lists

```
[ 1, 2, 3 ]
[ "a", "b", "c" ]
[ true, false, true ]
```

You can also use whitespace without commas:

```
[ 1 2 3 ]
[ "a" "b" "c" ]
```

### Mixed Type Lists

```
[ 1, "value", 42, true ]
[ "string", 123, false ]
```

### Nested Lists

Lists can contain other lists:

```
[ [ 1, 2 ], [ 3, 4 ] ]
[ "outer", [ "inner1", "inner2" ], "outer2" ]
```

### Recommended Formatting

For single-line lists, use commas for better readability:

```
[ 1, 2, 3 ]
[ "a", "b", "c" ]
[ 1, "value", 42, true ]
```

For multi-line lists, format with one item per line (commas optional):

```
[ 1
  2
  3 ]

[ 1
  "value"
  42
  true ]

[ [ 1, 2 ]
  [ 3, 4 ]
  [ 5, 6 ] ]
```

### Empty List

```
[ ]
```

## Map

A map is a collection of named values (key-value pairs). The key is always an identifier. Values can be of different types, and maps can be nested.

Commas are optional. You can separate map entries with whitespace or commas.

### Simple Maps

```
{ configuration: "Release" }
{ a: 1, b: 2 }
{ name: "app", version: 42, enabled: true }
```

### Mixed Type Values

```
{ 
  name: "myapp"
  version: 1
  enabled: true
  tags: [ "web", "api" ]
}
```

### Nested Maps

Maps can contain other maps and lists:

```
{ 
  app: { name: "myapp", version: 1 }
  config: { debug: true }
}

{ 
  servers: [ "server1", "server2" ]
  settings: { timeout: 30, retries: 3 }
}
```

### Complex Nested Structures

```
{
  projects: [ 
    { name: "api", path: "src/api" }
    { name: "web", path: "src/web" }
  ]
  defaults: {
    config: "Release"
    platforms: [ "linux", "windows" ]
  }
}
```

### Recommended Formatting

For readability, format maps with one entry per line (multi-line format is recommended):

```
{ configuration: "Release"
  max: 42 }

{ a: 1
  b: 2
  c: 3 }

{ 
  app: { name: "myapp", version: 1 }
  config: { debug: true }
}
```

### Empty Map

```
{ }
```
