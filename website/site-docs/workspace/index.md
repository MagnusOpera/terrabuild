---
title: Workspace

---

A `WORKSPACE` file defines shared build and deployment policy. It sits at the workspace root.

The file defines configuration that applies to all projects in the workspace, including:
* Target dependencies and relationships
* Optional build phases and their ordering
* Default extension configurations
* Workspace-level variables
* Cache configuration

This section describes the syntax and configuration options for the `WORKSPACE` file. The WORKSPACE file uses the same Terrabuild configuration language as PROJECT, with workspace-specific blocks and attributes.
