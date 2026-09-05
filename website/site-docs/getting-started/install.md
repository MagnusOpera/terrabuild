---
title: Install Terrabuild
description: Install the Terrabuild CLI locally or in CI.
---

Install Terrabuild on a developer machine first. The `terrabuild` command should
then be available on your `PATH`.

## Homebrew on macOS or Linux

```bash
brew install magnusopera/tap/terrabuild
```

Upgrade it later with:

```bash
brew upgrade magnusopera/tap/terrabuild
```

## .NET global tool

If the .NET CLI is already installed:

```bash
dotnet tool install --global Terrabuild
```

Upgrade it with:

```bash
dotnet tool update --global Terrabuild
```

## Download a release

Download an archive from
[GitHub Releases](https://github.com/magnusopera/terrabuild/releases) for:

- macOS on Apple silicon;
- Linux on x64 or ARM64;
- Windows on x64 or ARM64;
- the platform-independent .NET tool.

Extract the executable into a directory on your `PATH`.

## GitHub Actions

CI needs the same Terrabuild version used by developers. On a runner with a
compatible .NET SDK already installed:

```yaml
- name: Install Terrabuild
  run: dotnet tool install --global Terrabuild --version 0.200.1
```

Use the version chosen by your repository and ensure the .NET global tools
directory is on `PATH`. Install the native tools used by your targets too, or
provide them through [extension containers](../extensibility/container.md).

## Check the installation

```bash
terrabuild version
terrabuild --help
```

Terrabuild needs a `WORKSPACE` file before it can run repository targets. The
[first workflow tutorial](./quick-start.md) walks you through creating one,
along with two small projects. It needs Git and a POSIX shell.

For your own repository, [scaffolding](./scaffolding.md) can create an initial
`WORKSPACE` and `PROJECT` files.

Local execution and caching require no account. Connect
[Insights](./insights.md) later if you want a shared delivery record and
encrypted artifact reuse across machines.
