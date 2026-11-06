---
title: Host functions
---

FScript provides the language and standard library documented in the [FScript manual](https://fscript.magnusopera.io/manual/). Terrabuild controls which host functions an extension may call.

Terrabuild grants FScript `IO` and `Threading` permissions. It does not grant `Interactive` or `Native`, so extensions cannot read terminal input or load external FFI providers. Function availability follows those capabilities rather than a name blacklist.

Tasks may run concurrently during extension evaluation. The runtime joins every task it starts before returning from script loading or a handler invocation, including tasks without an explicit await. Foreground and background failures are collected and fail evaluation. Independent tasks finish after a sibling fails. Host IO remains confined to the workspace policy; a task that never completes prevents evaluation from returning.

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

### Tasks

- `Task.spawn` and `Task.await`
- `Task.delay` and `Task.fromResult`
- `Task.continueWith`, `Task.whenAll`, and `Task.whenAny`
- `Task.awaitAll`

Explicit awaits retrieve results; runtime joining also covers unobserved tasks and losing `whenAny` candidates. Each task inherits the same workspace IO policy. Callbacks that access shared host state must be safe to run concurrently.

### Output

- `Console.writeLine`

Return [`ShellOperation`](./types#shelloperation) values to request commands. Host functions run while Terrabuild evaluates the extension. Shell operations run later under the graph's scheduling, container, caching, and failure rules.

For the language's built-in modules and functions, use the [FScript standard-library reference](https://fscript.magnusopera.io/manual/stdlib/overview).
