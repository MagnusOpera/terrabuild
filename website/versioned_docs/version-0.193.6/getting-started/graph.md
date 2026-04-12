---
title: Graph

prev: /docs/getting-started

---

At the heart of Terrabuild is a **DAG (Directed Acyclic Graph)** that represents your entire build. Understanding this graph is key to understanding how Terrabuild works.

## What is the Build Graph?

When you run `terrabuild run <target>`, Terrabuild analyzes your workspace and builds a graph where:

- **Nodes** represent tasks (e.g., "build project A's build target")
- **Edges** represent dependencies (e.g., "project A depends on project B")
- The graph is **directed** - dependencies flow in one direction
- The graph is **acyclic** - no circular dependencies are allowed

This graph structure enables Terrabuild to:
- Determine what needs to be built and in what order
- Identify what can be built in parallel
- Skip building unchanged projects
- Use cached artifacts when available

## Example: How Projects Become a Graph

The following example shows how multiple projects with dependencies become a build graph. Each project has its own `PROJECT` file, and dependencies are typically discovered automatically by Terrabuild's extensions (though you can also specify them explicitly).

This example is from the [Terrabuild Playground](https://github.com/MagnusOpera/Terrabuild-Playground) - a sample workspace you can use to experiment.

```mermaid
flowchart LR
  classDef default fill:moccasin,stroke:black
  classDef project fill:gainsboro,stroke:black,rx:10,ry:10
  classDef build fill:palegreen,stroke:black,rx:20,ry:20
  classDef publish fill:skyblue,stroke:black,rx:20,ry:20
  classDef target fill:white,stroke-width:2px,stroke-dasharray: 5 2

  subgraph A[".net WebApi"]
    subgraph targetBuildA["target build"]
      direction LR
      buildA(["@dotnet build"])
    end

    subgraph targetPublishA["target dist"]
      direction LR
      publishA(["@dotnet publish"]) --> dockerA(["@docker build"])
    end

    class targetBuildA build
    class targetPublishA publish
  end

  subgraph B["Vue.js WebApp"]
    subgraph targetBuildB["target build"]
      direction LR
      buildB(["@npm build"])
    end

    subgraph targetPublishB["target dist"]
      direction LR
      dockerB(["@docker build"])
    end

    class targetBuildB build
    class targetPublishB publish
  end

  subgraph C[".net Library"]
    subgraph targetBuildC["target build"]
      direction LR
      buildC(["@dotnet build"])
    end

    class targetBuildC build
  end

  subgraph D["Typescript Library"]
    subgraph targetBuildD["target build"]
      direction LR
      buildD(["@npm build"])
    end

    class targetBuildD build
  end

  publish([deploy])
  targetBuildB --> targetBuildD
  targetBuildA --> targetBuildC
  targetPublishA --> targetBuildA
  publish -.-> targetPublishA
  
  targetPublishB --> targetBuildB
  publish -.-> targetPublishB

  class A,B,C,D project
  class publish target
```

Build graph is as follow:
```mermaid
flowchart LR
classDef forced stroke:red,stroke-width:3px
classDef required stroke:orange,stroke-width:3px
classDef selected stroke:black,stroke-width:3px
942179365051C1A33DCF747C64D41786C8982DD5C9B7EB7D8792D5AB26BC93EE([build src/apps/webapi])
2472A7D7AFE84797E8835F7F8AAA9671A230BC0391CD60D47D68F0982D660F4D([build src/libs/cslib])
942179365051C1A33DCF747C64D41786C8982DD5C9B7EB7D8792D5AB26BC93EE --> 2472A7D7AFE84797E8835F7F8AAA9671A230BC0391CD60D47D68F0982D660F4D
class 942179365051C1A33DCF747C64D41786C8982DD5C9B7EB7D8792D5AB26BC93EE required
class 2472A7D7AFE84797E8835F7F8AAA9671A230BC0391CD60D47D68F0982D660F4D required
6AE17A5B59E873CC19549EC37FF0E70529F5ACF9199EF788A57AA36D515D5305([plan src/deploy])
class 6AE17A5B59E873CC19549EC37FF0E70529F5ACF9199EF788A57AA36D515D5305 required
A4784F53304358C5B2A731D8ADEA338F8DE2217105C7A0B1D76E9CE3536D946F([build src/apps/webapp])
09B7D90F73A18A551759DF0B8F900113268C2015C4B23E930DC1EF5427F55EA4([build src/libs/tslib])
A4784F53304358C5B2A731D8ADEA338F8DE2217105C7A0B1D76E9CE3536D946F --> 09B7D90F73A18A551759DF0B8F900113268C2015C4B23E930DC1EF5427F55EA4
class A4784F53304358C5B2A731D8ADEA338F8DE2217105C7A0B1D76E9CE3536D946F required
class 09B7D90F73A18A551759DF0B8F900113268C2015C4B23E930DC1EF5427F55EA4 required
C84DDA0307CE672A87A67571C61AF0A56E74CC8C6AB291EB1AAF863E0EAE4905([dist src/apps/webapi])
C84DDA0307CE672A87A67571C61AF0A56E74CC8C6AB291EB1AAF863E0EAE4905 --> 942179365051C1A33DCF747C64D41786C8982DD5C9B7EB7D8792D5AB26BC93EE
class C84DDA0307CE672A87A67571C61AF0A56E74CC8C6AB291EB1AAF863E0EAE4905 required
20A918F5A24BDC971D2453B59EB07784463ABFFE026049F1A1BB5C0BE1F06576([deploy src/deploy])
20A918F5A24BDC971D2453B59EB07784463ABFFE026049F1A1BB5C0BE1F06576 --> C84DDA0307CE672A87A67571C61AF0A56E74CC8C6AB291EB1AAF863E0EAE4905
20A918F5A24BDC971D2453B59EB07784463ABFFE026049F1A1BB5C0BE1F06576 --> F7DB4DD0555F1851A6EDF90789A946B7D9385F94186559AC6124CEC556EA1B44
20A918F5A24BDC971D2453B59EB07784463ABFFE026049F1A1BB5C0BE1F06576 --> 6AE17A5B59E873CC19549EC37FF0E70529F5ACF9199EF788A57AA36D515D5305
class 20A918F5A24BDC971D2453B59EB07784463ABFFE026049F1A1BB5C0BE1F06576 required
F7DB4DD0555F1851A6EDF90789A946B7D9385F94186559AC6124CEC556EA1B44([dist src/apps/webapp])
F7DB4DD0555F1851A6EDF90789A946B7D9385F94186559AC6124CEC556EA1B44 --> A4784F53304358C5B2A731D8ADEA338F8DE2217105C7A0B1D76E9CE3536D946F
class F7DB4DD0555F1851A6EDF90789A946B7D9385F94186559AC6124CEC556EA1B44 required
```

## How the Graph Enables Fast Builds

The graph structure enables Terrabuild's core optimization: **only build what changed**.

When you run a build:
1. Terrabuild checks each node in the graph
2. If a project's files or dependencies changed, that project needs building
3. If nothing changed, the project can be restored from cache (see [Caching](/docs/getting-started/caching))
4. Dependent projects are automatically marked for build if their dependencies changed

This means:
- **Most builds are fast** - Only changed projects are built
- **Branches share cache** - Same code on different branches reuses cache
- **Dependencies are handled automatically** - If a library changes, apps using it are built

The graph is what makes Terrabuild efficient for monorepos. Even with hundreds of projects, you typically only build a handful on each change.
