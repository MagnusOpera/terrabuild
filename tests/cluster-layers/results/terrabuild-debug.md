# Options
 * Targets: build
* Workspace: /Users/pct/src/MagnusOpera/Terrabuild/tests/cluster-layers
* Force: True
* Retry: False
* MaxConcurrency: 2
* LocalOnly: True
* ContainerTool: docker
* WhatIf: False
* Debug: True

# Build Graph
```mermaid
flowchart TD
classDef build stroke:red,stroke-width:3px
classDef restore stroke:orange,stroke-width:3px
classDef ignore stroke:black,stroke-width:3px
a:build("<b>build</b> 
A")
b:build("<b>build</b> 
B")
c:build("<b>build</b> 
C")
d:build("<b>build</b> 
D")
e:build("<b>build</b> 
E")
f:build("<b>build</b> 
F")
g:build("<b>build</b> 
G")
class a:build ignore
class b:build ignore
c:build --> a:build
c:build --> b:build
class c:build ignore
d:build --> c:build
class d:build ignore
e:build --> c:build
class e:build ignore
f:build --> d:build
f:build --> e:build
class f:build ignore
g:build --> c:build
class g:build ignore
```
