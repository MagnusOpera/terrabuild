---
title: Project

---

A `PROJECT` file defines one buildable or deployable unit. It sits at the root of the project folder.

A project consists of:
* A required `PROJECT` file defines targets and project metadata.
* Committed files below the project path form the default tracked input set. Terrabuild applies `.gitignore` while discovering them.

This section describes the syntax and configuration options for the `PROJECT` file. The PROJECT file uses Terrabuild's configuration language to define targets, dependencies, outputs, phase assignments, and extension configurations. Phases themselves are declared in [`WORKSPACE`](../workspace/phase).
