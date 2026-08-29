---
title: Host functions
---

FScript provides the language and standard library documented in the [FScript manual](https://fscript.magnusopera.io/manual/0.78.1/). Terrabuild controls which host functions an extension may call.

Terrabuild exposes the default FScript host registry except for concurrency and interactive-input functions that do not fit deterministic build-graph evaluation:

- `Task.spawn`
- `Task.await`
- `Console.readLine`

## Functions available to extensions

### Filesystem

- `Fs.readText`
- `Fs.exists`
- `Fs.kind`
- `Fs.createDirectory`
- `Fs.writeText`
- `Fs.combinePath`
- `Fs.parentDirectory`
- `Fs.extension`
- `Fs.fileNameWithoutExtension`
- `Fs.glob`
- `Fs.enumerateFiles`

Filesystem access is confined to the workspace root. Paths matching `workspace.deny` are unavailable; the default deny list is `[ ".git" ]`.

### Data and utilities

- `Regex.matchGroups`
- `Hash.md5`
- `Guid.new`
- `Json.deserialize`
- `Json.serialize`
- `Xml.queryValues`

### Output

- `Console.writeLine`

Return [`ShellOperation`](./types#shelloperation) values to request commands. Host functions run while Terrabuild evaluates the extension. Shell operations run later under the graph's scheduling, container, caching, and failure rules.

For the language's built-in modules and functions, use the [FScript standard-library reference](https://fscript.magnusopera.io/manual/0.78.1/stdlib/overview).
