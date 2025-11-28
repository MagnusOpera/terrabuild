# Configuration

| Option | Value |
|--------|-------|
| Targets | apply build plan test |
| Force | True |
| LocalOnly | True |
| MaxConcurrency | 1 |
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
workspace/path|B:apply("<b>apply b</b> 
B")
workspace/path|B:plan("<b>plan b</b> 
B")
workspace/path|C:build("<b>build c</b> 
C")
class workspace/path|A:build ignore
workspace/path|B:apply --> workspace/path|B:plan
class workspace/path|B:apply ignore
class workspace/path|B:plan ignore
class workspace/path|C:build ignore
```

