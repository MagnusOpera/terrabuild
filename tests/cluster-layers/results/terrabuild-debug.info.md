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
workspace/path(d):build("<b>build d</b> 
D")
workspace/path(e):build("<b>build e</b> 
E")
workspace/path(f):build("<b>build f</b> 
F")
workspace/path(g):build("<b>build g</b> 
G")
class workspace/path(a):build ignore
class workspace/path(b):build ignore
workspace/path(c):build --> workspace/path(a):build
workspace/path(c):build --> workspace/path(b):build
class workspace/path(c):build ignore
workspace/path(d):build --> workspace/path(c):build
class workspace/path(d):build ignore
workspace/path(e):build --> workspace/path(c):build
class workspace/path(e):build ignore
workspace/path(f):build --> workspace/path(d):build
workspace/path(f):build --> workspace/path(e):build
class workspace/path(f):build ignore
workspace/path(g):build --> workspace/path(c):build
class workspace/path(g):build ignore
```

