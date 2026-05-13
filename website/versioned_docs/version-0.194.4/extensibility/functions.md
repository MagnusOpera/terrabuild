---
title: Functions
---

Terrabuild exposes the default FScript host registry except for the following functions, which are intentionally unavailable in extension scripts:

- `Task.spawn`
- `Task.await`
- `Console.readLine`

Available host functions:

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
- `Regex.matchGroups`
- `Hash.md5`
- `Guid.new`
- `Console.writeLine`
- `Json.deserialize`
- `Json.serialize`
- `Xml.queryValues`
