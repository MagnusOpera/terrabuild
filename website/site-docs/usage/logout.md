---
title: logout
---

`terrabuild logout` removes saved workspace token credentials from this workstation or CI job.

```text
USAGE: terrabuild logout [--help] --space <space>
```

## Example

```bash
terrabuild logout --space my-space
```

Use `terrabuild insights logout` to revoke an Insights user session. See [Login](./login) for managing the separately imported workspace decryption key.
