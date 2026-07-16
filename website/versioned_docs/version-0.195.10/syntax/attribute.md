---
title: Attribute

---

Attributes are used in [blocks](/docs/syntax/block) to configure behavior. An attribute has a name and is bound to an [expression](/docs/expression) which in turn is evaluated to the expected value.

```
# this is an attribute initialized with a list of string
environments = [ "staging", "dev*" ]

# this attribute initialize specific dependencies
depends_on = [ target.build
               target.init ]

# target dependencies are declared as a list
depends_on = [ target.gen target.clean ]

# this is an attribute initialized with a mapping
defaults = {
    arguments: { version: var.configuration }
    image: "ghcr.io/example/" + terrabuild.project
}
```
