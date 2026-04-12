---
title: Project

---

A `PROJECT` file is required to describe how to build a project. This file must be located at the root of the project folder.

A project consists of:
* A `PROJECT` file (mandatory) - defines the build configuration
* Committed files below the project path, determined using `.gitignore` - these are the source files that will be built

This section describes the syntax and configuration options for the `PROJECT` file. The PROJECT file uses an HCL-inspired syntax to define targets, dependencies, outputs, and extension configurations.
