# Contributing to Terrabuild

Thanks for contributing to Terrabuild and helping make it better. We appreciate the help!

## Communications

You are welcome to join the [Terrabuild Community Slack](https://terrabuild.io/community/) for questions and feature requests.
We discuss features and file bugs on GitHub via [Issues](https://github.com/magnusopera/terrabuild/issues).

### Issues

Feel free to pick up any existing issue that looks interesting to you or fix a bug you stumble across while using Terrabuild. No matter the size, we welcome all improvements.

Please keep in mind Terrabuild is a young product. We are doing our best to move forward while keeping product simple.

### Feature Work

For larger features, we'd appreciate it if you open a [new issue](https://github.com/magnusopera/terrabuild/issues/new) before investing a lot of time so we can discuss the feature together.
Please also be sure to browse [current issues](https://github.com/magnusopera/terrabuild/issues) to make sure your issue is unique, to lighten the triage burden on our maintainers.
Finally, please limit your pull requests to contain only one feature at a time. Separating feature work into individual pull requests helps speed up code review and reduces the barrier to merge.

## Developing

### Setting up your Terrabuild development environment

You'll want to install the following on your machine:

- [.net SDK](https://dotnet.microsoft.com/download)
- [GNU Make](https://www.gnu.org/software/make/)
- [Node.js](https://nodejs.org/) and [pnpm](https://pnpm.io/)
- [Docker](https://www.docker.com/products/docker-desktop/) or [OrbStack](https://orbstack.dev/)

### Build and validation

We use `make` as shortcuts to run commands.

We develop mainly on macOS and Ubuntu, with limited support for doing development on Windows. Feel free to pitch in if you can help improve that.

`Makefile` contains several targets. The most useful day-to-day targets are:
1. `build`: build Terrabuild. This is the default target.
2. `parser`: rebuild parser and start build.
3. `test`: build and run tests.
4. `publish`: build and publish.
5. `self`: publish and run Terrabuild against itself (`build test dist`).
6. `smoke-tests`: run integration scenarios under `tests/` and compare debug output snapshots.
7. `try-docs`: run DocGen in dry-run mode and validate extension docs metadata parsing.
8. `terrabuild`: run `build test dist` using the globally installed Terrabuild.

You probably also want to install current Terrabuild distribution: `dotnet tool install --global terrabuild`

Use `dotnet tool install --global --prerelease terrabuild` if you want pre-release version instead.

### Recommended validation order

For extension/script protocol changes:
1. `make try-docs`
2. `make test`
3. `make smoke-tests`
4. `make self`

For broad/core runtime changes:
1. `make test`
2. `make self`
3. `make smoke-tests`
4. `make try-docs`

If `make smoke-tests` produces diffs in `tests/*/results`, investigate and explain the root cause before merging.

## Submitting a Pull Request

For contributors we use the standard GitHub workflow: fork, create a branch and when ready, open a pull request from your fork.

### Changelog messages

Changelog notes are written in the active imperative form. They should not end with a period. The simple rule is to pretend the message starts with "This change will ..."

Good examples for changelog entries are:
- move whatif at task level
- invalidate local cache on cache inconsistency

Here's some examples of what we're trying to avoid:
- Fixes a bug
- Adds a feature
- Feature now does something

### Magnus Opera employees

Magnus Opera employees have write access to Magnus Opera repositories and must push directly to branches rather than forking the repository. Tests can run directly without approval for PRs based on branches rather than forks.

## Release process

Terrabuild uses a draft-based release flow:

1. Run `make release-prepare version=X.Y.Z` (stable) or `make release-prepare version=X.Y.Z-next` (preview).
2. Push commit and tag with `git push origin main --follow-tags`.
3. Wait for CI to create a draft GitHub release and upload artifacts.
4. Publish that existing draft release.

`make release-prepare` supports `X.Y.Z` and `X.Y.Z-next` only. Use `dryrun=true` to validate release preparation without writing changes.

## Getting Help

We're sure there are rough edges and we appreciate you helping out. If you want to talk with other folks in the Terrabuild community (including members of the Magnus Opera team) come hang out in the `#contribute` channel on the [Terrabuild Community Slack](https://terrabuild.io/community/).
