---
title: Connect Insights
description: Connect Terrabuild to the hosted delivery record and encrypted shared cache.
---

[Terrabuild Insights](../insights/index.md) is the optional hosted delivery record for
Terrabuild.

## What Insights adds

A connected workspace gains:

- execution and delivery-group history across developer machines and CI;
- interactive task graphs and execution context;
- environment timelines and release notes;
- GitHub-enriched delivery and engineering signals through Pulse;
- encrypted managed artifacts shared between authorized machines.

Terrabuild continues to provide its graph, runner, and local cache without an
Insights account.

## Connection policy

The workspace either runs locally or participates in the full Insights reporting
lifecycle:

- Without matching workspace credentials, Terrabuild does not create an
  Insights client. The run and cache remain local.
- Once Terrabuild connects, opening the run, publishing its graph and artifact
  metadata, and completing its record are part of the command. A reporting
  failure fails the command instead of silently leaving an incomplete record.

Remote artifact contents have a narrower failure policy. A missing, corrupt, or
unreadable blob is a cache miss. A failed transfer does not discard a completed
local cache entry.

## 1. Create an Insights workspace

Sign in to [Insights](https://insights.magnusopera.io), create a workspace, and
open **Integrations → Terrabuild**. Insights provides the workspace block to add
to the repository.

```hcl {filename="WORKSPACE"}
workspace {
  id = "<workspace-id>"
}
```

The workspace ID is not a secret. It identifies the Insights workspace that
receives Terrabuild data.

## 2. Create credentials

Create a workspace-scoped contributor token under **Settings → Security**. It is
shown once. Store it with the master key in your password manager or CI secret
store.

- The token authorizes Terrabuild to report runs and publish artifacts.
- The master key encrypts and decrypts managed artifact contents on the
  Terrabuild machine.

Losing the master key makes previously uploaded artifacts unreadable. Insights
does not receive the plaintext key.

## 3. Authenticate Terrabuild

Run:

```bash
terrabuild login \
  --workspace "<workspace-id>" \
  --token "<token>" \
  --masterkey "<master-key>"
```

The workspace ID must match `workspace.id`. Credentials are saved in the local
Terrabuild profile for that workspace.

## 4. Publish managed artifacts

Reporting works for every connected run. To share a target's declared files,
set its artifact policy to `~managed`:

```hcl {filename="WORKSPACE"}
target build {
  artifacts = ~managed
  depends_on = [ target.^build ]
}
```

Terrabuild compresses and encrypts the selected files before upload. Another
connected machine with the same master key can restore them when it computes the
same target identity.

Use `~external` for artifacts owned by a registry and `~none` for side effects.
See [Caching](./caching) for the complete ownership model.

## 5. Group related commands

A CI workflow may build and plan before an approval, then apply in a separate
job. Give both commands the same group identifier:

```bash
terrabuild run build test dist plan \
  --environment production \
  --group "${GITHUB_REPOSITORY}:${GITHUB_RUN_ID}"

terrabuild run deploy \
  --environment production \
  --group "${GITHUB_REPOSITORY}:${GITHUB_RUN_ID}"
```

Insights presents them as one delivery group while preserving the two execution
records.

## CI example

Store the token and master key as protected secrets and authenticate before the
run:

```yaml
- name: Connect Terrabuild to Insights
  env:
    TERRABUILD_TOKEN: ${{ secrets.TERRABUILD_TOKEN }}
    TERRABUILD_MASTER_KEY: ${{ secrets.TERRABUILD_MASTER_KEY }}
  run: |
    terrabuild login \
      --workspace "<workspace-id>" \
      --token "$TERRABUILD_TOKEN" \
      --masterkey "$TERRABUILD_MASTER_KEY"

- name: Build and test
  run: terrabuild run build test --group "${{ github.repository }}:${{ github.run_id }}"

- name: Remove Terrabuild credentials
  if: always()
  run: terrabuild logout --space "<workspace-id>"
```

Use `--local-only` for a run that must neither report to Insights nor read or
write the managed cache. Local caching remains enabled.

## Next steps

- [The delivery record](../insights/delivery-record.md) explains run groups, graphs, and artifacts.
- [Environments and releases](../insights/environments-and-releases.md) follows changes through deployment points.
- [Pulse](../insights/pulse.md) combines Terrabuild and GitHub evidence.
