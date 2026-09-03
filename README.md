<a href="https://terrabuild.io?utm_campaign=magnusopera-terrabuild-github-repo&utm_source=github.com&utm_medium=top-logo" title="Terrabuild - Desired-state software delivery for monorepos">
    <img src="https://terrabuild.io/images/logo-name.svg" height="50" />
</a>

<br>

[![License](https://img.shields.io/github/license/magnusopera/terrabuild)](LICENSE.md)
[![NuGet version](https://badge.fury.io/nu/terrabuild.svg)](https://www.nuget.org/packages/Terrabuild)
![build](https://github.com/magnusopera/terrabuild/actions/workflows/on-push-branch.yml/badge.svg?branch=main)

# What is Terrabuild?

Terrabuild brings desired-state configuration to software delivery in polyglot
monorepos. You declare the outcome—built, tested, packaged, planned, or
deployed—and the relationships that make it valid.

Terrabuild resolves that intent into one dependency graph, recognizes results
that already satisfy it, and runs the remaining work in dependency order. Your
existing tools still compile, test, package, and deploy. The same declaration
runs on a developer machine and in CI.

- Declare delivery outcomes and dependencies with concise HCL-like files.
- Build and test independent projects concurrently while respecting their dependencies.
- Connect application artifacts to environment-specific plans and deployments.
- Reuse deterministic outputs locally or through the optional Insights service.
- Inspect a run before executing it and compare delivery impact with an earlier commit.
- Keep existing .NET, Node.js, Docker, Terraform, and other project configuration.

# Terrabuild and Insights

Terrabuild converges the delivery graph toward the requested state.
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
