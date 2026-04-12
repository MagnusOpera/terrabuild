---
title: Variables

---

Variables used in expressions have a scope and an identifier. The scope defines the source of the variable, and the identifier is the name of the variable within that scope.

Terrabuild supports the following variable scopes:
* **`terrabuild`** - [Predefined variables](/docs/expression/predefined-variables) provided by Terrabuild (build context, system info, etc.)
* **`variable`** - [Workspace variables](/docs/workspace/variable) defined in the WORKSPACE file
* **`local`** - [Local values](/docs/workspace/locals) defined in WORKSPACE or [PROJECT](/docs/project/locals) files
* **`project`** - [Project properties](/docs/project/project) from the project block

Here are some variable reference examples:
* `terrabuild.branch_or_tag` - Access the current Git branch or tag
* `var.config` - Access a workspace variable named `config`
* `local.config` - Access a local value named `config`
* `project.api` - Access a project with identifier `api`
