---
title: login
---

`terrabuild login` connects the local machine or CI environment to an Insights workspace.

```text
USAGE: terrabuild login [--help] --workspace <id> --token <token>
                        --masterkey <masterKey>
```

## Example

```bash
terrabuild login --workspace <workspace-id> --token <token> --masterkey <master-key>
```

After login, Terrabuild can use shared cache, upload build metadata, and resolve features such as `impact`.
