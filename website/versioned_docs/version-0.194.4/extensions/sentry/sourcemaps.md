---
title: "sourcemaps"
---
Injects and uploads sourcemaps for JavaScript bundles using Debug IDs.
```
@sentry sourcemaps {
    project = "insights"
    path = "dist"
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | external
| Bach      | no

## Argument Reference
The following arguments are supported:
* `project` - (Optional) Sentry project slug.
* `path` - (Optional) Directory with sourcemaps. Default value is `dist`.
