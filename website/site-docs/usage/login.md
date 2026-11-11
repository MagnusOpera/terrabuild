---
title: login
---

`terrabuild login` connects a workstation or CI job to an Insights workspace using a workspace token. The token's permissions determine whether Terrabuild can publish artifacts, report builds, and resolve features such as `impact`.

```bash
terrabuild login --workspace "$WORKSPACE_ID" --token "$TERRABUILD_TOKEN" --masterkey "$TERRABUILD_MASTER_KEY"
terrabuild run build
terrabuild logout --space "$WORKSPACE_ID"
```

On a workstation, Terrabuild stores the token and the derived artifact key in macOS Keychain, Windows Credential Manager, or Linux Secret Service. Linux requires the `secret-tool` executable from libsecret and an available desktop keyring. Unlock the keyring and retry if the store is unavailable.

In GitHub Actions, Terrabuild stores the credentials in a private directory under `RUNNER_TEMP` so they are available to later steps in the same job. For another ephemeral CI runner, set `TERRABUILD_CI_CREDENTIAL_DIR` to an absolute, private directory shared by the steps of that job and removed at job teardown. Provide this variable in the job environment. Keep the directory outside the repository, CI caches, and job artifacts. `terrabuild logout --space` removes saved token credentials for that workspace, including credentials for different Insights origins.

## Download artifacts with an Insights account

```bash
terrabuild insights login
terrabuild insights import-key --workspace "$WORKSPACE_ID"
terrabuild run build --restore
```

Approve the device code in the browser, then enter the workspace master key at the hidden prompt. The import command also accepts a single line from stdin. The session and derived decryption key are stored separately in the OS credential store.

A run using the Insights session can download artifacts. Cache misses build and save locally. If you also saved a workspace token, use `terrabuild run build --publish` to explicitly select token-based publishing. `--local-only` bypasses remote caching and build reporting.

Use `terrabuild insights logout` to revoke the session and `terrabuild insights forget-key --workspace "$WORKSPACE_ID"` to remove the imported decryption key.

## Upgrading from plaintext credentials

Remove the old `~/.terrabuild/config.json` and `~/.terrabuild/insights-*.json` files, then run the appropriate login command again. For Insights account access, import the workspace key again as well.
