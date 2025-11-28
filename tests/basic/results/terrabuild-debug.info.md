# Configuration

| Option | Value |
|--------|-------|
| Targets | build |
| Environment | dev |
| Force | True |
| LocalOnly | True |
| MaxConcurrency | 2 |
| Engine | docker |
| Debug | True |

# Build Graph

```mermaid
flowchart TD
classDef build stroke:red,stroke-width:3px
classDef restore stroke:orange,stroke-width:3px
classDef ignore stroke:black,stroke-width:3px
[path]:app:build("<b>build appprj</b> 
app")
class [path]:app:build ignore
```

