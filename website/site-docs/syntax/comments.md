---
title: Comments

---

Comments start with a `#`. Content is then ignored until the end of the line.

```
# This is a target
target build {
    @dotnet publish { log = true }
    @docker build
}

# Declare and configure terraform extension
# Following environment variables are
# passed to container
extension @terraform {
    image = "hashicorp/terraform:1.8.4" # Extension is running in a container
    variables = [ "ARM_TENANT_ID"
                  "ARM_SUBSCRIPTION_ID"
                  "ARM_CLIENT_ID"
                  "ARM_CLIENT_SECRET"
                  "ARM_ACCESS_KEY" ]
}
```
