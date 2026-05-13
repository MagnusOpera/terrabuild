---
title: "test"
---
Playwright extension for end-to-end tests.
```
@playwright test {
    browser = "webkit"
    project = "ci"
    args = "--grep @smoke"
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | remote
| Bach      | no

## Argument Reference
The following arguments are supported:
* `browser` - (Optional) Browser name.
* `project` - (Optional) Project name.
* `args` - (Optional) Extra `playwright test` arguments.
