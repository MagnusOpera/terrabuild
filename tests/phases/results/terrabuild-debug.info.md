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
subgraph phase_application_1FE28920["Phase: application"]
phase_application_1FE28920_gate{{"application"}}
#pnpm#phase-app:build("<b>build app</b> 
apps/app")
end
subgraph phase_toolchains_8B7A6256["Phase: toolchains"]
phase_toolchains_8B7A6256_gate{{"toolchains"}}
workspace/path#tools/pnpm:dist("<b>dist pnpm</b> 
tools/pnpm")
end
phase_application_1FE28920_gate -.-> phase_toolchains_8B7A6256_gate
#pnpm#phase-app:build --> workspace/path#tools/pnpm:dist
class #pnpm#phase-app:build ignore
class workspace/path#tools/pnpm:dist ignore
```

