---
title: "generate"
---
Generates an API client via `openapi-generator-cli generate`.
```
@openapi generate {
    generator = "typescript-axios"
    input = "src/api.json"
    output = "src/api/client"
    properties = { withoutPrefixEnums: "true" }
    args = "--type-mappings ClassA=ClassB"
}
```
### Capabilities

| Capability | Info |
|------------|------|
| Cache      | remote
| Bach      | no

## Argument Reference
The following arguments are supported:
* `generator` - (Required) Generator name.
* `input` - (Required) Input OpenAPI file.
* `output` - (Required) Output directory.
* `properties` - (Optional) Generator properties (`--additional-properties`).
* `args` - (Optional) Extra generator arguments.
