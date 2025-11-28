# Configuration

| Option | Value |
|--------|-------|
| Targets | build test |
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
76E6F8566C567ED61454D9A9EC6ED7D096DBD92DDB5CAD796F9B7EB5EB3595C6("<b>build</b> 
.")
E26E627E00DC63984574A9CFAD93DBAC058522CE652BB3DC444648A748AB900A("<b>build</b> 
.")
[path]:deployments/terraform-deploy:build("<b>build</b> 
deployments/terraform-deploy")
[path]:libraries/dotnet-lib:build("<b>build</b> 
libraries/dotnet-lib")
[path]:libraries/npm-lib:build("<b>build</b> 
libraries/npm-lib")
[path]:libraries/shell-lib:build("<b>build shell_lib</b> 
libraries/shell-lib")
[path]:projects/dotnet-app:build("<b>build dotnet_app</b> 
projects/dotnet-app")
[path]:projects/make-app:build("<b>build</b> 
projects/make-app")
[path]:projects/npm-app:build("<b>build npm_app</b> 
projects/npm-app")
[path]:projects/open-api:build("<b>build</b> 
projects/open-api")
[path]:projects/rust-app:build("<b>build</b> 
projects/rust-app")
[path]:tests/playwright:test("<b>test playwright_test</b> 
tests/playwright")
class 76E6F8566C567ED61454D9A9EC6ED7D096DBD92DDB5CAD796F9B7EB5EB3595C6 ignore
class E26E627E00DC63984574A9CFAD93DBAC058522CE652BB3DC444648A748AB900A ignore
[path]:deployments/terraform-deploy:build --> [path]:projects/dotnet-app:build
[path]:deployments/terraform-deploy:build --> [path]:projects/npm-app:build
class [path]:deployments/terraform-deploy:build ignore
class [path]:libraries/dotnet-lib:build ignore
class [path]:libraries/npm-lib:build ignore
class [path]:libraries/shell-lib:build ignore
[path]:projects/dotnet-app:build --> [path]:libraries/dotnet-lib:build
class [path]:projects/dotnet-app:build ignore
[path]:projects/make-app:build --> [path]:libraries/shell-lib:build
class [path]:projects/make-app:build ignore
[path]:projects/npm-app:build --> [path]:libraries/npm-lib:build
class [path]:projects/npm-app:build ignore
class [path]:projects/open-api:build ignore
class [path]:projects/rust-app:build ignore
class [path]:tests/playwright:test ignore
```

