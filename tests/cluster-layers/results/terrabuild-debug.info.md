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
[path]:D:build("<b>build d</b> 
D")
[path]:E:build("<b>build e</b> 
E")
[path]:F:build("<b>build f</b> 
F")
[path]:G:build("<b>build g</b> 
G")
class [path]:A:build ignore
class [path]:B:build ignore
[path]:C:build --> [path]:A:build
[path]:C:build --> [path]:B:build
class [path]:C:build ignore
[path]:D:build --> [path]:C:build
class [path]:D:build ignore
[path]:E:build --> [path]:C:build
class [path]:E:build ignore
[path]:F:build --> [path]:D:build
[path]:F:build --> [path]:E:build
class [path]:F:build ignore
[path]:G:build --> [path]:C:build
class [path]:G:build ignore
```

