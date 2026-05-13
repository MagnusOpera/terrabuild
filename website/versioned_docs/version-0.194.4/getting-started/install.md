---
title: Install

prev: /docs/getting-started/batch

---

## Download and install

Several deployment scenario exists:
* Brew
* Download from GitHub Release
* Install using GitHub Actions
* Install as a .net tool

After installation, Terrabuild is available in your **PATH**. You can then invoke the command `terrabuild`.

### Brew (macOS/Linux)

Brew is a package manager for macOS (or Linux), see more at https://brew.sh

To install Terrabuild, run following command:
```
brew install magnusopera/tap/terrabuild
```

To upgrade Terrabuild, run following command:
```
brew upgrade magnusopera/tap/terrabuild
```

#### GitHub Release

Download Terrabuild from [GitHub Releases](https://github.com/magnusopera/terrabuild/releases).

Select your target platform, Terrabuild is available for following platforms:
* MacOS (darwin-x64 / darwin-arm64)
* Linux (linux-x64 / linux-arm64)
* Windows (windows-x64)
* It's also available as a universal dotnet tool

:::warning
If you are providing your own extensions, you must install .net sdk 10 or more.
:::

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

If you have dotnet-cli install (comes with SDK), you can install Terrabuild as a tool:
```
dotnet tool install --global Terrabuild
```

To upgrade, run following command (or just run previous command):
```
dotnet tool update --global Terrabuild
```

## Configuration

In order to build your monorepos, some work is required and several files have to be added:
* **WORKSPACE**: file at the root of your workspace, it describes [common configuration](/docs/workspace) for your project.
* **PROJECT**: add one at the root of each project, it describes [configuration](/docs/project) to build the project.

Probably a bit tedious to do this by hand if you have numerous projects in your monorepo 😓. Terrabuild ships with a [scaffolding](/docs/getting-started/scaffolding) tool. this will get you on the fast track!

## Create an Insights account and enable caching

Terrabuild relies on a backend for sharing artifacts across builds. for this, you need to create an account and a workspace on [Insights](https://insights.magnusopera.io). You will then be able to share artifacts with allowed members in your space.

## Want to give it a ride ?

A [playground sample repository](https://github.com/MagnusOpera/terrabuild-playground) is available to quickly check if your configuration is correct. This will also give you the opportunity to read [WORKSPACE](/docs/workspace) and [PROJECT](/docs/project) files before heading into the documentation.
