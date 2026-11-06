---
title: compare
---

`terrabuild compare` explains cache-key and cache-decision differences between two saved diagnostic reports. It works offline, outside a workspace, and never executes targets or contacts Insights.

```text
USAGE: terrabuild compare [--help] --before <path> --after <path> [--out <path>]
```

## Capture and compare runs

Retain the diagnostic JSON before starting another debug run, which replaces `terrabuild-debug.json`:

```bash
terrabuild run build test --debug
cp terrabuild-debug.json before.json
# Make the change to investigate, then run the same selection again.
terrabuild run build test --debug
terrabuild compare --before before.json --after terrabuild-debug.json
```

For GitHub builds, download the `terrabuild-debug.json` files from the two runs' diagnostic artifacts. Compare corresponding jobs with the same target and project selection for the most direct explanation. The command also accepts partial reports; targets without resolved fingerprints, including nodes missing from an incomplete graph, are marked `unresolved` rather than treated as confirmed additions or removals.

Both inputs must use diagnostic schema version 6. Unsupported schemas, malformed reports, and duplicate project-directory/target identities are rejected. Reports from different machine checkout paths can be compared because matching uses workspace-relative paths. Added and removed targets are reported explicitly; renamed projects are not guessed.

## Read the output

The summary counts changed, added, removed, unresolved, and unchanged cache keys. Details identify changed files, project dependencies, target dependencies, target definitions, operation fingerprints, and evaluated input names. Only hashes and names are compared; raw command arguments, injected environment values, and credentials are not printed or copied into the comparison.

A changed execution decision with an unchanged key is reported separately. For example, `exec (cache-miss)` followed by `restore (cache-hit)` shows cache availability changing without a source change. A changed key is not proof that a target executed; inspect the run's action and final outcome alongside this comparison.

## Structured output

```bash
terrabuild compare --before before.json --after after.json --out reports/cache-diff.json
```

The JSON uses comparison `schemaVersion: 1`, `counts`, and a sorted `nodes` array. Each node includes its kind, before/after keys and actions, decision-change flag, reasons, and changed file/dependency/input hashes. An output path equal to either input is rejected. Finding differences is successful and returns exit code 0; invalid arguments or reports fail the command.
