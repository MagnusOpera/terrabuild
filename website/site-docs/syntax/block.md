---
title: Block

---

A block is the primary construction element in configuration. It helps structure an intent.
A block is composed of [attributes](/docs/syntax/attribute) followed by nested blocks.

Blocks are specific to [WORKSPACE](/docs/workspace) and [PROJECT](/docs/project) configurations.

```
target build {
    @dotnet publish { log = true }
    @docker build
}

extension @terraform {
    image = "hashicorp/terraform:1.8.4"
    variables = [ "ARM_TENANT_ID"
                  "ARM_SUBSCRIPTION_ID"
                  "ARM_CLIENT_ID"
                  "ARM_CLIENT_SECRET"
                  "ARM_ACCESS_KEY" ]
}
```
