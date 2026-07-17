---
title: Install

prev: /docs/getting-started

---

## Download and install

Terrabuild can be installed in several ways:
* Brew
* Download from GitHub Release
* Install using GitHub Actions
* Install as a .net tool

After installation, Terrabuild is available in your **PATH**. You can then invoke the command `terrabuild`.

### Brew (macOS/Linux)

Brew is a package manager for macOS and Linux. See [brew.sh](https://brew.sh) for details.

To install Terrabuild, run:
```
brew install magnusopera/tap/terrabuild
```

To upgrade Terrabuild, run:
```
brew upgrade magnusopera/tap/terrabuild
```

#### GitHub Release

Download Terrabuild from [GitHub Releases](https://github.com/magnusopera/terrabuild/releases).

Select the archive for your platform:
* macOS (darwin-arm64)
* Linux (linux-x64 / linux-arm64)
* Windows (windows-x64)
* Universal .NET tool

#### GitHub Actions

You can install Terrabuild in your GitHub Actions Workflow using binaries available on GitHub Release:
```yaml
- name: Install Terrabuild
  uses: jaxxstorm/action-install-gh-release
  with:
    repo: magnusopera/terrabuild
    # tag: 0.185.18
    platform: linux
```

#### Dotnet / NuGet

If the .NET CLI is installed, you can install Terrabuild as a global tool:
```
dotnet tool install --global Terrabuild
```

To upgrade:
```
dotnet tool update --global Terrabuild
```

## Configuration

Terrabuild uses two configuration files:
* **WORKSPACE**: one file at the repository root containing [shared configuration](/docs/workspace).
* **PROJECT**: one file at the root of each buildable unit containing [project configuration](/docs/project).

For an existing monorepo, the [scaffold command](/docs/getting-started/scaffolding) can generate a useful starting point.

## Optional: enable shared caching

Local caching works immediately and does not require an account. To share encrypted artifacts and build metadata across developers and CI, create a workspace on [Insights](https://insights.magnusopera.io) and follow the credentials provided there.

## Try it

The [Terrabuild Playground](https://github.com/MagnusOpera/terrabuild-playground) is a ready-to-run workspace. Continue with the [Quick Start](/docs/getting-started/quick-start) to build it.
