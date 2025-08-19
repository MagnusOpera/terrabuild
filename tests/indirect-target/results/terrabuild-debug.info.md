# Configuration

| Option | Value |
|--------|-------|
| Targets | apply build plan test |
| Force | True |
| LocalOnly | True |
| MaxConcurrency | 1 |
| ContainerTool | docker |
| Debug | True |

# Build Graph

```mermaid
flowchart TD
classDef build stroke:red,stroke-width:3px
classDef restore stroke:orange,stroke-width:3px
classDef ignore stroke:black,stroke-width:3px
a:build("<b>build a</b> 
A")
b:apply("<b>apply b</b> 
B")
b:plan("<b>plan b</b> 
B")
c:build("<b>build c</b> 
C")
class a:build ignore
b:apply --> b:plan
class b:apply ignore
class b:plan ignore
class c:build ignore
```

