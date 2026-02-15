# Terrabuild Validation Guide

This repository uses three primary validation commands.

## 1. Full validation: `make self`

Run from repository root:

```bash
make self
```

What it does:
- Cleans build outputs.
- Builds the UI.
- Builds/packs Terrabuild.
- Runs Terrabuild against itself.
- Executes project-level validation chain.

Use when:
- You changed core logic, build orchestration, scripting/runtime integration, or release-critical behavior.

Success criteria:
- Command exits with code `0`.

## 2. Integration validation: `make smoke-tests`

Run from repository root:

```bash
make smoke-tests
```

What it does:
- Runs integration scenarios under `tests/`.
- Validates end-to-end behavior of targets/extensions in representative projects.

Use when:
- You changed extension scripts, scripting protocol, graph behavior, or command dispatch.

Success criteria:
- All smoke test suites complete successfully.
- Command exits with code `0`.

## 3. Documentation generation check: `make try-docs`

Run from repository root:

```bash
make try-docs
```

What it does:
- Executes DocGen in dry-run style for Terrabuild extension documentation generation.
- Validates script metadata/doc parsing.

Use when:
- You changed extension script signatures, export syntax, doc comments, DocGen logic, or extension protocol docs.

Success criteria:
- Doc generation completes without exceptions.
- Command exits with code `0`.

## 4. Unit tests: `make test`

Run from repository root:

```bash
make test
```

What it does:
- Runs Terrabuild unit/integration test projects in the .NET solution.
- Validates language, configuration, expressions, scripting, and core behavior at test-project level.

Use when:
- You changed F# source code in `src/` (runtime, parser, config, graph, scripting, helpers).

Success criteria:
- All test projects pass.
- Command exits with code `0`.

## Recommended validation order

For extension and scripting changes:
1. `make try-docs`
2. `make test`
3. `make smoke-tests`
4. `make self`

For broad/core changes:
1. `make test`
2. `make self`
3. `make smoke-tests`
4. `make try-docs`

## Notes

- Run commands from the repository root (the directory containing `Makefile`).
- Some flows rely on Docker and local tooling (`dotnet`, `pnpm`, `node`), so transient environment issues can occur.
- If `make self` fails due environment/toolchain issues, still run `make smoke-tests` and `make try-docs` to isolate regressions from infrastructure noise.
- If `make smoke-tests` produces diffs, they **MUST** be investigated before accepting changes.
  The diff analysis should explain the root cause clearly (for example: file/content change, configuration change, or variable/input change).

## Release Process (Tags and GitHub Draft)

Terrabuild uses a draft-based release flow:

1. Run `make release version=X.Y.Z` (stable) or `make release version=X.Y.Z-next` (preview).
   - Optional preview mode: `make release version=X.Y.Z[-next] dryrun=true`
2. The command updates `CHANGELOG.md`, commits changelog changes, and creates a local tag.
3. Push commit and tag:
   - `git push origin main`
   - `git push origin X.Y.Z` (or `X.Y.Z-next`)
4. Wait for CI (`on-push-tag.yml`) to create the GitHub release as draft and upload artifacts.
5. Publish that existing draft release (do not create/publish manually before CI draft creation).

Rules:

- Tag workflow sources draft release notes from `CHANGELOG.md` section `## [X.Y.Z]`.
- Tag workflow fails if version section is missing/empty, has no bullet, or has no compare link.
- `on-release-published.yml` consumes release artifacts from the published release, signs macOS binaries, and publishes NuGet/Homebrew.
- `make release` supports `X.Y.Z` and `X.Y.Z-next` only.
- Compare link policy:
  - stable release compares against previous stable tag.
  - preview release compares against previous `-next` tag.
