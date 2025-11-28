# Configuration

| Option | Value |
|--------|-------|
| Targets | build |
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
workspace/path|A:build("<b>build a</b> 
A")
workspace/path|B:build("<b>build b</b> 
B")
workspace/path|C:build("<b>build c</b> 
C")
workspace/path|A:build --> workspace/path|B:build
workspace/path|A:build --> workspace/path|C:build
class workspace/path|A:build ignore
workspace/path|B:build --> workspace/path|C:build
class workspace/path|B:build ignore
class workspace/path|C:build ignore
```

