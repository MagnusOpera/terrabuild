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
[path]:A:build("<b>build a</b> 
A")
[path]:B:build("<b>build b</b> 
B")
[path]:C:build("<b>build c</b> 
C")
[path]:A:build --> [path]:B:build
[path]:A:build --> [path]:C:build
class [path]:A:build ignore
[path]:B:build --> [path]:C:build
class [path]:B:build ignore
class [path]:C:build ignore
```

