---
title: Variables

---

Variables used in expressions have a scope and an identifier. The scope defines the source of the variable, and the identifier is the name of the variable within that scope.

Terrabuild supports the following variable scopes:
* `terrabuild` contains [predefined values](/docs/expression/predefined-variables) supplied by Terrabuild.
* `var` contains [workspace variables](/docs/workspace/variable) declared in `WORKSPACE`.
* `local` contains values declared in [`WORKSPACE`](/docs/workspace/locals) or [`PROJECT`](/docs/project/locals).
* `project` contains [project properties](/docs/project/project) from the project block.

Here are some variable reference examples:
* `terrabuild.branch_or_tag` - Access the current Git branch or tag
* `var.config` - Access a workspace variable named `config`
* `local.config` - Access a local value named `config`
* `project.api.version` - Access the version hash of the project identified as `api`
