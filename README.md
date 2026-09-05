<a href="https://terrabuild.io?utm_campaign=magnusopera-terrabuild-github-repo&utm_source=github.com&utm_medium=top-logo" title="Terrabuild - Desired-state configuration for build and deployment">
    <img src="https://terrabuild.io/images/logo-name.svg" height="50" />
</a>

<br>

[![License](https://img.shields.io/github/license/magnusopera/terrabuild)](LICENSE.md)
[![NuGet version](https://badge.fury.io/nu/terrabuild.svg)](https://www.nuget.org/packages/Terrabuild)
![build](https://github.com/magnusopera/terrabuild/actions/workflows/on-push-branch.yml/badge.svg?branch=main)

# What is Terrabuild?

Terrabuild applies **desired-state configuration (DSC) to build and deployment**.
Start with the outcome you want: applications built,
tests passed, packages produced, or an environment deployed. You declare the
relationships and policies that make that outcome valid; Terrabuild determines
the work needed to reach it.

Coordination is how Terrabuild realizes that intent. It connects tools, projects,
and environment settings through one dependency model that runs locally and in CI.

Your existing tools do the work. Terrabuild determines their prerequisites,
runs independent work concurrently, and reuses matching results according to
explicit input, output, and execution policies.

- Describe shared rules in `WORKSPACE` and project commands in `PROJECT` files.
- Start with included integrations for .NET, Node.js, Docker, Go, and more.
- Configure tool arguments, container images, and environment-specific settings.
- Add or replace integrations under custom names using FScript extensions.
- Keep common builds reusable while plans and deployments consume their environment context.
- Inspect the selected workflow with `terrabuild explain` before executing it.

[Start with two small projects](https://terrabuild.io/docs/next/getting-started/quick-start),
then [adopt your existing tools](https://terrabuild.io/docs/next/getting-started/existing-repository)
and [coordinate environments](https://terrabuild.io/docs/next/getting-started/environments).
The first tutorial requires only Terrabuild, Git, and a POSIX shell.

# Terrabuild and Insights

Terrabuild executes the requested workflow.
[Insights](https://insights.magnusopera.io) records the states that were reached,
how they changed, and what each environment received.

Connect a workspace to share encrypted artifacts, inspect execution graphs,
follow changes through environments, generate release notes, and study delivery
and engineering trends alongside GitHub activity. Terrabuild remains fully
usable with its local cache when Insights is not connected.

# Learn more
- [Documentation](https://terrabuild.io/docs/?utm_campaign=magnusopera-terrabuild-github-repo&utm_source=github.com&utm_medium=docs)
- [Why Terrabuild](https://terrabuild.io/docs/next/getting-started/why-terrabuild/?utm_campaign=magnusopera-terrabuild-github-repo&utm_source=github.com&utm_medium=positioning)
- [Quickstart](https://terrabuild.io/docs/next/getting-started/quick-start/?utm_campaign=magnusopera-terrabuild-github-repo&utm_source=github.com&utm_medium=quickstart)
- [Insights](https://terrabuild.io/docs/next/insights/?utm_campaign=magnusopera-terrabuild-github-repo&utm_source=github.com&utm_medium=insights)
- [Playground](https://github.com/magnusopera/terrabuild-playground)
- [Local architecture docs](docs/README.md)

# Open source
This repository contains the Terrabuild software, covered under the [Functional Source License, Version 1.1, Apache 2.0 Future License](LICENSE.md), except where noted.

Terrabuild is a product produced from this open source software, exclusively by [Magnus Opera SAS](https://magnusopera.io). It is distributed under our commercial terms.

Others are allowed to make their own distribution of the software, but they cannot use any of the Terrabuild trademarks, cloud services, etc.

We explicitly grant permission for you to make a build that includes our trademarks while developing the Terrabuild software itself. You may not publish or share the build, and you may not use that build to run Terrabuild software for any other purpose.

# Contributing
Visit [Contributing](CONTRIBUTING.md) for information on building Terrabuild from source or contributing improvements.

<a href="https://terrabuild.io/docs/?utm_campaign=magnusopera-terrabuild-github-repo&utm_source=github.com&utm_medium=get-started-button" title="Get Started">
    <img src="https://terrabuild.io/images/get-started.svg" />
</a>
