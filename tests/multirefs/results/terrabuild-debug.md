# Terrabuild
* Version: v0.0.0+931083a8c2acb31fb5b454c5fc2f20eb4b29d11f
* Location: /Users/pct/src/MagnusOpera/Terrabuild/src/Terrabuild/bin/Debug/net9.0/terrabuild.dll

# Options
* StartedAt: 8/18/2025 10:25:02 AM
 * Targets: build
* Workspace: /Users/pct/src/MagnusOpera/Terrabuild/tests/multirefs
* Force: True
* Retry: False
* MaxConcurrency: 2
* LocalOnly: True
* BranchOrTag: feature/mermaid-markdown
* HeadCommit: 931083a8c2acb31fb5b454c5fc2f20eb4b29d11f
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
a:build --> b:build
a:build --> c:build
class a:build ignore
b:build --> c:build
class b:build ignore
class c:build ignore
```
