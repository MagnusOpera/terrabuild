---
title: prune
---

`terrabuild prune` removes local build cache entries that have not been accessed recently.

```text
USAGE: terrabuild prune [--help] <days>
```

## Example

```bash
terrabuild prune 14
```

This removes local cache entries that have not been accessed for more than `14` days. It also removes old staging directories abandoned by a terminated Terrabuild process. A staging directory whose exclusive lease is still held by a live producer is always preserved.
