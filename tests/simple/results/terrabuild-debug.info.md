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
14A78A1501AFF6333D1FF5A6011BEACBBF52A6C04FB7B5882B117B7F486A46D9("<b>build</b> 
.")
577FDA009FE819C8E72C3183372BF7070AA7BB84CB823855E17AF5C0EE960D4B("<b>build</b> 
.")
#pnpm#npm-app:build("<b>build npm_app</b> 
projects/npm-app")
#pnpm#npm-lib:build("<b>build npm_lib</b> 
libraries/npm-lib")
workspace/path#deployments/terraform-deploy:build("<b>build</b> 
deployments/terraform-deploy")
workspace/path#libraries/dotnet-lib:build("<b>build</b> 
libraries/dotnet-lib")
workspace/path#libraries/shell-lib:build("<b>build shell_lib</b> 
libraries/shell-lib")
workspace/path#projects/dotnet-app:build("<b>build dotnet_app</b> 
projects/dotnet-app")
workspace/path#projects/make-app:build("<b>build</b> 
projects/make-app")
workspace/path#projects/open-api:build("<b>build</b> 
projects/open-api")
workspace/path#projects/rust-app:build("<b>build</b> 
projects/rust-app")
workspace/path#tests/playwright:test("<b>test playwright_test</b> 
tests/playwright")
class 14A78A1501AFF6333D1FF5A6011BEACBBF52A6C04FB7B5882B117B7F486A46D9 ignore
class 577FDA009FE819C8E72C3183372BF7070AA7BB84CB823855E17AF5C0EE960D4B ignore
#pnpm#npm-app:build --> #pnpm#npm-lib:build
class #pnpm#npm-app:build ignore
class #pnpm#npm-lib:build ignore
workspace/path#deployments/terraform-deploy:build --> #pnpm#npm-app:build
workspace/path#deployments/terraform-deploy:build --> workspace/path#projects/dotnet-app:build
class workspace/path#deployments/terraform-deploy:build ignore
class workspace/path#libraries/dotnet-lib:build ignore
class workspace/path#libraries/shell-lib:build ignore
workspace/path#projects/dotnet-app:build --> workspace/path#libraries/dotnet-lib:build
class workspace/path#projects/dotnet-app:build ignore
workspace/path#projects/make-app:build --> workspace/path#libraries/shell-lib:build
class workspace/path#projects/make-app:build ignore
class workspace/path#projects/open-api:build ignore
class workspace/path#projects/rust-app:build ignore
class workspace/path#tests/playwright:test ignore
```

