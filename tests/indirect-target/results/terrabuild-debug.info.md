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
workspace/path#a:build("<b>build a</b> 
A")
workspace/path#b:apply("<b>apply b</b> 
B")
workspace/path#b:plan("<b>plan b</b> 
B")
workspace/path#c:build("<b>build c</b> 
C")
class workspace/path#a:build ignore
workspace/path#b:apply --> workspace/path#b:plan
class workspace/path#b:apply ignore
class workspace/path#b:plan ignore
class workspace/path#c:build ignore
```

