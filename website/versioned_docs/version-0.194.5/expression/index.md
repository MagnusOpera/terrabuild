---
title: Expression

---

Expressions are used to configure actions and provide dynamic values in Terrabuild configuration files. They are lazily evaluated - computed right before invoking an action - and are strongly typed: the type is inferred automatically based on the expression's result.

Expressions support [variables](/docs/expression/variables) and [functions](/docs/expression/functions), allowing you to create dynamic configurations that adapt based on context, environment, or other variables.
