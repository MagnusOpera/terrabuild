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

```terrabuild title="WORKSPACE"
workspace {
  id = "<workspace-id>"
}
```

The workspace ID is not a secret. It identifies the Insights workspace that
receives Terrabuild data.

## 2. Initialize the workspace and approve users

Link the repository in Insights. Complete your account identity setup and save
the Recovery Kit when prompted. In workspace **Security**, unlock your artifact
identity and choose **Initialize workspace key** as the workspace Owner.

Other users complete their own account setup. A decrypting Owner or Admin opens
**Security** and selects **Grant workspace access** beside the user's account.
The grant applies on every machine using that account identity. Replacement
identities can receive existing workspace roots through the same page.

On your computer, run:

```bash
terrabuild insights login
```

Approve the displayed code in Insights. Unlock with your Recovery Kit if needed.
The CLI receives the identity through an encrypted handoff and saves it in the
OS credential store. No device enrollment or per-device key management is needed.

## 3. Provision CI

A workspace Owner or Admin opens **Security**, unlocks their artifact identity,
and selects **Create CI token**. Choose a name and whether the job may publish.
The browser generates the token locally, proves possession of its derived
wrapping and signing keys, registers it, and grants workspace access.

Copy the token directly into the CI provider's secret store as `TERRABUILD_TOKEN`.
It is displayed once. Save it before dismissing the panel. Jobs that only restore
artifacts should leave publishing permission disabled.

Issue a different token for each Insights origin and workspace. Insights receives
a derived authentication credential and public keys. The raw token can derive
the private keys, so anyone who obtains it can use that CI identity.

A revoked token stops authenticating immediately. Previously signed artifact
URLs can work until they expire, and revocation cannot remove keys or files
already copied. A decrypting administrator can rotate the workspace key from
**Security**. If every holder loses the root, the Owner can reset it there and
rebuild the remote cache. Anyone holding a workspace root can decrypt every
artifact encrypted under it across that workspace's repositories.

## 4. Publish managed artifacts

A workspace contributor or administrator can publish using an approved account key or contributor CI token. To share a
target's declared files, set its artifact policy to `~managed`:

```terrabuild title="WORKSPACE"
target build {
  artifacts = ~managed
  depends_on = [ target.^build ]
}
```

Terrabuild compresses and encrypts the selected files before upload. Another
connected machine with a workspace grant can restore them when it computes the
same target identity in the same repository.

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

Only the login step needs the CI secret.

```yaml
- name: Connect Terrabuild to Insights
  env:
    TERRABUILD_TOKEN: ${{ secrets.TERRABUILD_TOKEN }}
  run: |
    printf '%s\n' "$TERRABUILD_TOKEN" | terrabuild login --workspace "<workspace-id>"

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
