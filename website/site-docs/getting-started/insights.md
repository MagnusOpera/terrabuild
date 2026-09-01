---
title: Insights

prev: /docs/getting-started/caching

---

[Insights](https://insights.magnusopera.io) is the optional managed service for Terrabuild. Terrabuild works without an account and always provides a local cache; connecting a workspace adds build history, graph snapshots, and encrypted cache sharing across developers and CI.

“Optional” means that a workspace chooses whether to connect; it does not make reporting best-effort after connection. The policy is binary:

- Without matching workspace credentials, Terrabuild does not create an Insights client and the run remains local.
- Once Terrabuild connects, starting the build, uploading its graph and artifact metadata, and completing its report are part of the run. A failure in that lifecycle fails the Terrabuild command instead of silently producing an incomplete Insights record.

Remote artifact contents have a narrower cache policy. A missing, corrupt, or unreadable cached blob is a cache miss, and a failed artifact transfer does not discard the completed local cache entry. That fallback concerns reusable artifact data; it does not turn Insights build reporting into an optional background operation.

## What Insights adds

When a connected `terrabuild run` starts, Terrabuild reports:

- the repository, commit, branch or tag, and available CI run identity
- the requested targets, configuration, environment, and run options
- the selected build graph and its project and target hashes
- task results, execution windows, and whether cached artifacts were built or reused

Targets configured with `artifacts = ~managed` can also upload their logs and outputs to the managed cache. Another machine with the same workspace credentials can restore those artifacts when Terrabuild computes the same cache key.

This gives a team one place to inspect builds and their graphs while avoiding repeated work across machines and branches. Graph snapshots also enable [`terrabuild impact`](/docs/usage/impact), which compares the current graph with a graph stored for an earlier commit.

## Connect a workspace

### 1. Create the Insights workspace

Sign in to [Insights](https://insights.magnusopera.io), create or select a workspace, and obtain these values:

- **Workspace ID** identifies the shared workspace.
- **Token** authorizes Terrabuild to connect to it.
- **Master key** encrypts and decrypts managed artifacts on the Terrabuild machine.

Treat the token and master key as secrets. Every developer or CI runner that must share managed artifacts needs the same master key. Store it in a password manager or CI secret store: losing it makes previously uploaded artifacts unusable.

### 2. Identify the workspace in `WORKSPACE`

Add the Insights workspace ID to the repository's `WORKSPACE` file:

```hcl {filename="WORKSPACE"}
workspace {
    id = "<workspace-id>"
}
```

Terrabuild uses this ID to select the matching credentials on each machine. Keeping the ID in source control is expected; do not put the token or master key in `WORKSPACE`.

### 3. Save credentials on the machine

Run:

```bash
terrabuild login \
  --workspace "<workspace-id>" \
  --token "<token>" \
  --masterkey "<master-key>"
```

The workspace ID passed to `login` must match `workspace.id`. Login saves the credentials in the local Terrabuild profile, so it normally needs to be done only once per developer machine. A later login for the same workspace replaces its saved credentials.

### 4. Enable managed artifacts

Build reporting works whenever the workspace is connected. To share a target's cached logs and outputs as well, set its artifact mode to `~managed`:

```hcl {filename="WORKSPACE"}
target build {
    artifacts = ~managed
    depends_on = [ target.^build ]
}
```

The target's configured `outputs` determine which output files Terrabuild preserves. See [Caching](/docs/getting-started/caching) and the [Target Block reference](/docs/workspace/target) for cache and output configuration.

### 5. Run Terrabuild

Run a target normally:

```bash
terrabuild run build
```

Terrabuild prints `Connected to Insights` before preparing the graph when it finds credentials for the configured workspace. The run and graph then appear in Insights, and managed targets can reuse matching artifacts uploaded by other connected machines.

## Artifact encryption

Terrabuild compresses and encrypts managed logs and outputs locally before uploading them. Downloads are decrypted locally with the saved master key. The master key is not part of the build metadata sent when Terrabuild opens an Insights build.

The token and master key have different jobs: rotating a token changes access to the workspace, while changing the master key changes which managed artifacts that machine can decrypt. Coordinate master-key changes across the team.

## Describe and group runs

Optional `run` arguments make related work easier to identify in Insights:

```bash
terrabuild run deploy \
  --environment production \
  --group "release-2026.08" \
  --tag "v2026.08" \
  --note "Production release"
```

- `--group` assigns the same identifier to related Terrabuild invocations.
- `--tag` attaches a label to the build.
- `--note` adds human context.

Use a stable group value when a pipeline invokes Terrabuild more than once but those invocations belong to one delivery or workflow.

## Use Insights in CI

Store the token and master key as protected CI secrets, log in before the build, and remove the saved credentials afterward. For GitHub Actions:

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

- name: Build
  run: terrabuild run build

- name: Remove Terrabuild credentials
  if: always()
  run: terrabuild logout --space "<workspace-id>"
```

The checkout must contain the same `workspace.id`. Terrabuild automatically includes supported source-control and GitHub Actions context in the reported build.

## Temporarily stay local

Use `--local-only` for a run that must neither use the managed cache nor report to Insights:

```bash
terrabuild run build --local-only
```

Local caching remains available.

To remove one workspace's saved credentials from the current machine, run:

```bash
terrabuild logout --space "<workspace-id>"
```

Logout does not change `WORKSPACE`, delete local cache entries, or remove data already stored in Insights.

Continue with [Batch](/docs/getting-started/batch) to learn when grouping compatible tasks can outperform task-by-task execution.
