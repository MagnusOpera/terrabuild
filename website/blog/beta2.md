---
title: Beta 2
date: 2024-09-30
excludeSearch: true
tags:
  - Terrabuild
---

**Terrabuild** is getting a beta2 ! Here are new features:
* Extensions in containers
* Notarization for macOS
* links
* Storage tracking
* New property
* Optimizer removal

Latest release are available on [GitHub](https://github.com/MagnusOpera/Terrabuild/releases).

## Extensions in containers
Extensions can run in a container, this is great because this means zero deployments on dev environment but Terrabuild and Docker. Unfortunately, they used to be unreliable: build systems are not designed to be isolated this way. Investigations have led to shared memory usage. By enabling `--pid=host` and `--ipc=host` on containers, builds are now much more reliable now. Still not perfect but it's getting close!

## Notarization for macOS
`Terrabuild` is now notarized for macOS and compiled to universal binary. Still, if you want to create your own extension (using F# fsx), you still need to install .net SDK alongside.

Releases starting from 0.88 are notarized and universal binaries for macOS.

## Links support
Links are a new addition to projects. The goal is to set a dependency between two projects but build order is not enforced. This is usefull when you know your CI is acting as a build barrier and you do not want to enforce a build order. A link allows you to use `version` function for example but without requiring target outputs to be downloaded.

## Storage tracking
Storage consumption is now tracked and enforced. NOTE: API incompatibilities has been introduced so please, upgrade to 0.90 before. For evaluation, max storage is 1 Gb and is enforced for free accounts. Contact us if you need more for evaluation.

# New property
`terrabuild_ci` nows tells if build is running is CI. Note a of now only GitHub is supported.

## Optimizer removal
Sad news but the optimizer had to go away. Feature was hard to maintain and had too many ramifications across the board. The implication is build can be slower for .net projects (that was the only kind of project supporting optimizer). Not a big deal as only part of monorepo is built normally.

Removal of this feature will allow us to implement new ones. Stay tuned !
