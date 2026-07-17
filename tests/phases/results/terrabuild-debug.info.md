# Configuration

| Option | Value |
|--------|-------|
| Targets | build |
| Projects | app |
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
#pnpm#phase-app:build("<b>build app</b> 
apps/app")
workspace/path#tools/pnpm:dist("<b>dist pnpm</b> 
tools/pnpm")
#pnpm#phase-app:build --> workspace/path#tools/pnpm:dist
class #pnpm#phase-app:build ignore
class workspace/path#tools/pnpm:dist ignore
```

