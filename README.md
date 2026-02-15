<a href="https://terrabuild.io?utm_campaign=magnusopera-terrabuild-github-repo&utm_source=github.com&utm_medium=top-logo" title="Terrabuild - Monorepo build tool">
    <img src="https://terrabuild.io/images/logo-name.svg" height="50" />
</a>

<br>

[![License](https://img.shields.io/github/license/magnusopera/terrabuild)](LICENSE.md)
[![NuGet version](https://badge.fury.io/nu/terrabuild.svg)](https://www.nuget.org/packages/Terrabuild)
![build](https://github.com/magnusopera/terrabuild/actions/workflows/on-push-branch.yml/badge.svg?branch=main)

# What is Terrabuild ?
Terrabuild is a tool to maintain and build efficiently monorepos. Terrabuild is language agnostic: it just knows about dependencies and how to build a project:

- describe a workspace (collection of several projects) using a familiar syntax (HCL-like)
- focus on how to build a project and its dependencies: Terrabuild will optimize and run concurrent builds whenever possible
- Terrabuild ships with default extensions but you can implement your own as well
- Terrabuild uses heavy caching and several graph optimizations to make build fast both on CI and dev environment

# Benefits
- focus on project and the big picture, not the nitty-gritty details
- local build is same as CI one
- describe instead of tweak yaml
- ensure consistency and detect impacts earlier in development
- no lock-in: Terrabuild is not intrusive and can be used without modifying your projects

# Learn more
- [Documentation](https://terrabuild.io/docs/?utm_campaign=magnusopera-terrabuild-github-repo&utm_source=github.com&utm_medium=docs)
- [Quickstart](https://terrabuild.io/docs/getting-started/quick-start/?utm_campaign=magnusopera-terrabuild-github-repo&utm_source=github.com&utm_medium=quickstart)
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
