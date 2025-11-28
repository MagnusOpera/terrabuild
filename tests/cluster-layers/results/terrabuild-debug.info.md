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
workspace/path|D:build("<b>build d</b> 
D")
workspace/path|E:build("<b>build e</b> 
E")
workspace/path|F:build("<b>build f</b> 
F")
workspace/path|G:build("<b>build g</b> 
G")
class workspace/path|A:build ignore
class workspace/path|B:build ignore
workspace/path|C:build --> workspace/path|A:build
workspace/path|C:build --> workspace/path|B:build
class workspace/path|C:build ignore
workspace/path|D:build --> workspace/path|C:build
class workspace/path|D:build ignore
workspace/path|E:build --> workspace/path|C:build
class workspace/path|E:build ignore
workspace/path|F:build --> workspace/path|D:build
workspace/path|F:build --> workspace/path|E:build
class workspace/path|F:build ignore
workspace/path|G:build --> workspace/path|C:build
class workspace/path|G:build ignore
```

