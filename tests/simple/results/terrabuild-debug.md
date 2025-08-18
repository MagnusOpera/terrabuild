# Terrabuild
* Version: v0.0.0+931083a8c2acb31fb5b454c5fc2f20eb4b29d11f
* Location: /Users/pct/src/MagnusOpera/Terrabuild/src/Terrabuild/bin/Debug/net9.0/terrabuild.dll

# Options
* StartedAt: 8/18/2025 10:25:06 AM
 * Targets: build test
* Workspace: /Users/pct/src/MagnusOpera/Terrabuild/tests/simple
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
deployments/terraform-deploy:build("<b>build</b> 
deployments/terraform-deploy")
libraries/dotnet-lib:build("<b>build</b> 
libraries/dotnet-lib")
libraries/npm-lib:build("<b>build</b> 
libraries/npm-lib")
libraries/shell-lib:build("<b>build</b> 
libraries/shell-lib")
projects/dotnet-app:build("<b>build dotnet_app</b> 
projects/dotnet-app")
projects/make-app:build("<b>build</b> 
projects/make-app")
projects/npm-app:build("<b>build npm_app</b> 
projects/npm-app")
projects/open-api:build("<b>build</b> 
projects/open-api")
projects/rust-app:build("<b>build</b> 
projects/rust-app")
tests/playwright:test("<b>test playwright_test</b> 
tests/playwright")
deployments/terraform-deploy:build --> projects/dotnet-app:build
deployments/terraform-deploy:build --> projects/npm-app:build
class deployments/terraform-deploy:build ignore
class libraries/dotnet-lib:build ignore
class libraries/npm-lib:build ignore
class libraries/shell-lib:build ignore
projects/dotnet-app:build --> libraries/dotnet-lib:build
class projects/dotnet-app:build ignore
projects/make-app:build --> libraries/shell-lib:build
class projects/make-app:build ignore
projects/npm-app:build --> libraries/npm-lib:build
class projects/npm-app:build ignore
class projects/open-api:build ignore
class projects/rust-app:build ignore
class tests/playwright:test ignore
```
