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
workspace/path(a):build("<b>build a</b> 
A")
workspace/path(b):build("<b>build b</b> 
B")
workspace/path(c):build("<b>build c</b> 
C")
workspace/path(a):build --> workspace/path(b):build
workspace/path(a):build --> workspace/path(c):build
class workspace/path(a):build ignore
workspace/path(b):build --> workspace/path(c):build
class workspace/path(b):build ignore
class workspace/path(c):build ignore
```

